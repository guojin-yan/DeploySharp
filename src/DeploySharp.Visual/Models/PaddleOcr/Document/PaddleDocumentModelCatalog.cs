using System;
using System.Collections.Generic;
using System.Linq;

#pragma warning disable CS1591

namespace JYPPX.DeploySharp.Visual.Models.PaddleOcr.Document
{
    /// <summary>Official PaddleOCR/PP-Structure model catalog used for capability admission, not runtime auto-download. / 用于能力准入而非运行时自动下载的官方 PaddleOCR/PP-Structure 模型目录。</summary>
    public static class PaddleDocumentModelCatalog
    {
        public static IReadOnlyList<PaddleDocumentModelDescriptor> Official { get; } = new[]
        {
            new PaddleDocumentModelDescriptor("paddle-doc/pp-lcnet-x1-0-doc-ori", "PP-LCNet_x1_0_doc_ori", PaddleDocumentModule.DocumentOrientation, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-LCNet_x1_0_doc_ori_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-doc/uvdoc", "UVDoc", PaddleDocumentModule.TextImageUnwarping, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/UVDoc_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-doc/pp-doclayout-plus-l", "PP-DocLayout_plus-L", PaddleDocumentModule.LayoutDetection, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-DocLayout_plus-L_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-doc/pp-doclayout-l", "PP-DocLayout-L", PaddleDocumentModule.LayoutDetection, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-DocLayout-L_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-doc/pp-doclayout-m", "PP-DocLayout-M", PaddleDocumentModule.LayoutDetection, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-DocLayout-M_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-doc/pp-doclayout-s", "PP-DocLayout-S", PaddleDocumentModule.LayoutDetection, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-DocLayout-S_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-doc/pp-docblocklayout", "PP-DocBlockLayout", PaddleDocumentModule.LayoutDetection, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-DocBlockLayout_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-doc/picodet-layout-1x", "PicoDet_layout_1x", PaddleDocumentModule.LayoutDetection, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PicoDet_layout_1x_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-doc/picodet-layout-1x-table", "PicoDet_layout_1x_table", PaddleDocumentModule.LayoutDetection, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PicoDet_layout_1x_table_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-doc/picodet-s-layout-3cls", "PicoDet-S_layout_3cls", PaddleDocumentModule.LayoutDetection, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PicoDet-S_layout_3cls_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-doc/picodet-l-layout-3cls", "PicoDet-L_layout_3cls", PaddleDocumentModule.LayoutDetection, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PicoDet-L_layout_3cls_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-doc/rt-detr-h-layout-3cls", "RT-DETR-H_layout_3cls", PaddleDocumentModule.LayoutDetection, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/RT-DETR-H_layout_3cls_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-doc/picodet-s-layout-17cls", "PicoDet-S_layout_17cls", PaddleDocumentModule.LayoutDetection, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PicoDet-S_layout_17cls_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-doc/picodet-l-layout-17cls", "PicoDet-L_layout_17cls", PaddleDocumentModule.LayoutDetection, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PicoDet-L_layout_17cls_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-doc/rt-detr-h-layout-17cls", "RT-DETR-H_layout_17cls", PaddleDocumentModule.LayoutDetection, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/RT-DETR-H_layout_17cls_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-table/slanext-wired", "SLANeXt_wired", PaddleDocumentModule.TableStructureRecognition, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/SLANeXt_wired_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-table/slanext-wireless", "SLANeXt_wireless", PaddleDocumentModule.TableStructureRecognition, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/SLANeXt_wireless_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-table/pp-lcnet-x1-0-table-cls", "PP-LCNet_x1_0_table_cls", PaddleDocumentModule.TableClassification, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-LCNet_x1_0_table_cls_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-table/rt-detr-l-wired-cell-det", "RT-DETR-L_wired_table_cell_det", PaddleDocumentModule.TableCellDetection, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/RT-DETR-L_wired_table_cell_det_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-table/rt-detr-l-wireless-cell-det", "RT-DETR-L_wireless_table_cell_det", PaddleDocumentModule.TableCellDetection, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/RT-DETR-L_wireless_table_cell_det_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-formula/pp-formulanet-plus-s", "PP-FormulaNet_plus-S", PaddleDocumentModule.FormulaRecognition, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-FormulaNet_plus-S_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-formula/pp-formulanet-plus-m", "PP-FormulaNet_plus-M", PaddleDocumentModule.FormulaRecognition, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-FormulaNet_plus-M_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-formula/pp-formulanet-plus-l", "PP-FormulaNet_plus-L", PaddleDocumentModule.FormulaRecognition, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-FormulaNet_plus-L_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-formula/pp-formulanet-s", "PP-FormulaNet-S", PaddleDocumentModule.FormulaRecognition, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-FormulaNet-S_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-formula/pp-formulanet-l", "PP-FormulaNet-L", PaddleDocumentModule.FormulaRecognition, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-FormulaNet-L_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-formula/unimernet", "UniMERNet", PaddleDocumentModule.FormulaRecognition, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/UniMERNet_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-seal/ppocrv4-mobile", "PP-OCRv4_mobile_seal_det", PaddleDocumentModule.SealTextDetection, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-OCRv4_mobile_seal_det_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-seal/ppocrv4-server", "PP-OCRv4_server_seal_det", PaddleDocumentModule.SealTextDetection, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-OCRv4_server_seal_det_infer.tar", "paddle-inference"),
            new PaddleDocumentModelDescriptor("paddle-chart/pp-chart2table", "PP-Chart2Table", PaddleDocumentModule.ChartParsing, "https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/PP-Chart2Table_infer.tar", "paddle-inference")
        };

        /// <summary>Finds one catalog entry by stable model ID. / 按稳定模型 ID 查找目录项。</summary>
        public static PaddleDocumentModelDescriptor Get(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId)) throw new ArgumentException("A model id is required.", nameof(modelId));
            PaddleDocumentModelDescriptor? descriptor = Official.FirstOrDefault(model => string.Equals(model.ModelId, modelId.Trim(), StringComparison.Ordinal));
            if (descriptor == null) throw new KeyNotFoundException("The Paddle document model is not present in the official catalog: " + modelId);
            return descriptor;
        }

        /// <summary>Returns a stable read-only view of all catalog entries for one module. / 返回指定模块的稳定只读目录视图。</summary>
        public static IReadOnlyList<PaddleDocumentModelDescriptor> ForModule(PaddleDocumentModule module)
        {
            if (!Enum.IsDefined(typeof(PaddleDocumentModule), module)) throw new ArgumentOutOfRangeException(nameof(module));
            return Official.Where(model => model.Module == module).ToArray();
        }
    }
}
#pragma warning restore CS1591
