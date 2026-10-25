"""Check head grouping against complete attention, including dynamic Q/K lengths."""

import copy
import importlib.util
from pathlib import Path
import unittest

import numpy as np
import onnx
from onnx import TensorProto, helper
import onnxruntime as ort


SOURCE = Path(__file__).resolve().parents[1] / "Optimize-PaddleOcrVlVisionHeads.py"
SPEC = importlib.util.spec_from_file_location("vl_head_groups", SOURCE)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def model():
    graph = helper.make_graph([
        helper.make_node("MatMul", ["q", "kt"], ["scores"], name="scores"),
        helper.make_node("Softmax", ["scores"], ["probabilities"], axis=-1, name="probabilities"),
        helper.make_node("MatMul", ["probabilities", "v"], ["out"], name="out")], "attention", [
            helper.make_tensor_value_info("q", TensorProto.FLOAT, [1, 16, "query", 72]),
            helper.make_tensor_value_info("kt", TensorProto.FLOAT, [1, 16, 72, "key"]),
            helper.make_tensor_value_info("v", TensorProto.FLOAT, [1, 16, "key", 72])], [
            helper.make_tensor_value_info("out", TensorProto.FLOAT, [1, 16, "query", 72])])
    return helper.make_model(graph, opset_imports=[helper.make_opsetid("", 18)], ir_version=10)


def run(graph, inputs):
    options = ort.SessionOptions()
    options.intra_op_num_threads = 1
    options.inter_op_num_threads = 1
    return ort.InferenceSession(graph.SerializeToString(), options, providers=["CPUExecutionProvider"]).run(None, inputs)[0]


class HeadGroupTests(unittest.TestCase):
    def test_scaled_attention_matches_ort_fusion_order(self):
        original = model()
        scale = np.float32(0.34329453110694885)
        original.graph.initializer.append(helper.make_tensor("scale", TensorProto.FLOAT, [], [float(scale)]))
        nodes = list(original.graph.node)
        nodes[0].input[:] = ["scaled_q", "scaled_kt"]
        del original.graph.node[:]
        original.graph.node.extend([helper.make_node("Mul", ["q", "scale"], ["scaled_q"]),
                                    helper.make_node("Mul", ["kt", "scale"], ["scaled_kt"]), *nodes])
        random = np.random.default_rng(27)
        inputs = {"q": random.normal(size=(1, 16, 13, 72)).astype(np.float32),
                  "kt": random.normal(size=(1, 16, 72, 17)).astype(np.float32),
                  "v": random.normal(size=(1, 16, 17, 72)).astype(np.float32)}
        for schedule in ("loop", "unrolled"):
            grouped = copy.deepcopy(original)
            rows = MODULE.group_attention_heads(grouped, 2, expected_blocks=1, schedule=schedule)
            self.assertEqual(float(np.float32(scale * scale)), rows[0]["float32ScoreAlpha"])
            np.testing.assert_allclose(run(grouped, inputs), run(original, inputs), rtol=1e-6, atol=1e-6)

    def test_every_group_size_preserves_all_keys_and_dynamic_shapes(self):
        random = np.random.default_rng(20261024)
        for group_size, schedule in ((size, mode) for size in (1, 2, 4, 8) for mode in ("loop", "unrolled")):
            original = model()
            grouped = copy.deepcopy(original)
            rows = MODULE.group_attention_heads(grouped, group_size, expected_blocks=1, schedule=schedule)
            onnx.checker.check_model(grouped)
            self.assertEqual(16 // group_size, rows[0]["groups"])
            for query, key in ((1, 1), (3, 7), (9, 5)):
                with self.subTest(group=group_size, schedule=schedule, query=query, key=key):
                    inputs = {"q": random.normal(size=(1, 16, query, 72)).astype(np.float32) * 0.1,
                              "kt": random.normal(size=(1, 16, 72, key)).astype(np.float32),
                              "v": random.normal(size=(1, 16, key, 72)).astype(np.float32)}
                    actual, expected = run(grouped, inputs), run(original, inputs)
                    np.testing.assert_allclose(actual, expected, rtol=1e-6, atol=1e-6)
                    self.assertEqual((1, 16, query, 72), actual.shape)

    def test_loop_scans_attended_values_instead_of_scores(self):
        graph = model()
        MODULE.group_attention_heads(graph, 1, expected_blocks=1)
        loop = next(node for node in graph.graph.node if node.op_type == "Loop")
        body = next(attribute.g for attribute in loop.attribute if attribute.name == "body")
        self.assertEqual(5, len(body.output))
        self.assertEqual(5, len(loop.input))
        self.assertTrue(body.output[-1].name.endswith("/attended"))
        self.assertEqual(["MatMul", "Softmax", "MatMul"],
                         [node.op_type for node in body.node if node.op_type in ("MatMul", "Softmax")])
        self.assertFalse(any(node.op_type == "Softmax" for node in graph.graph.node))

    def test_query_chunks_preserve_tail_order_and_every_original_key(self):
        random = np.random.default_rng(20261025)
        for group_size in (1, 2, 4, 8):
            grouped = model()
            MODULE.group_attention_heads(grouped, group_size, expected_blocks=1, query_chunk_size=4)
            onnx.checker.check_model(grouped)
            for query, key in ((1, 7), (4, 3), (9, 5)):
                with self.subTest(group=group_size, query=query, key=key):
                    inputs = {"q": random.normal(size=(1, 16, query, 72)).astype(np.float32) * 0.1,
                              "kt": random.normal(size=(1, 16, 72, key)).astype(np.float32),
                              "v": random.normal(size=(1, 16, key, 72)).astype(np.float32)}
                    actual = run(grouped, inputs)
                    np.testing.assert_allclose(actual, run(model(), inputs), rtol=1e-6, atol=1e-6)
                    self.assertEqual((1, 16, query, 72), actual.shape)

    def test_invalid_schedule_is_rejected_without_mutation(self):
        graph = model()
        before = graph.SerializeToString()
        with self.assertRaisesRegex(ValueError, "schedule must"):
            MODULE.group_attention_heads(graph, 2, expected_blocks=1, schedule="parallel")
        self.assertEqual(before, graph.SerializeToString())

    def test_shared_intermediate_is_rejected_without_mutation(self):
        graph = model()
        graph.graph.output.append(helper.make_tensor_value_info("scores", TensorProto.FLOAT, [1, 16, "query", "key"]))
        before = graph.SerializeToString()
        with self.assertRaisesRegex(ValueError, "Public intermediate"):
            MODULE.group_attention_heads(graph, 2, expected_blocks=1)
        self.assertEqual(before, graph.SerializeToString())

    def test_wrong_softmax_axis_is_rejected(self):
        graph = model()
        graph.graph.node[1].attribute[0].i = 1
        with self.assertRaisesRegex(ValueError, "full final key axis"):
            MODULE.group_attention_heads(graph, 2, expected_blocks=1)

    def test_expected_block_count_and_repeat_rewrite_are_enforced(self):
        graph = model()
        before = graph.SerializeToString()
        with self.assertRaisesRegex(ValueError, "27 attention blocks"):
            MODULE.group_attention_heads(graph, 2)
        self.assertEqual(before, graph.SerializeToString())
        MODULE.group_attention_heads(graph, 2, expected_blocks=1)
        with self.assertRaisesRegex(ValueError, "already head-grouped"):
            MODULE.group_attention_heads(graph, 2, expected_blocks=1)


if __name__ == "__main__":
    unittest.main()
