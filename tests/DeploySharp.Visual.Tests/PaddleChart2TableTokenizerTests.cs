using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class PaddleChart2TableTokenizerTests
    {
        [TestMethod]
        public void OfficialQwenTiktokenPreservesPaddleChartPromptBoundaries()
        {
            string? root = Environment.GetEnvironmentVariable("DEPLOYSHARP_CHART2TABLE_MODEL_ROOT");
            if (string.IsNullOrWhiteSpace(root) || !File.Exists(Path.Combine(root, "qwen.tiktoken")))
            {
                Assert.Inconclusive("Set DEPLOYSHARP_CHART2TABLE_MODEL_ROOT to the official PP-Chart2Table directory.");
            }

            var tokenizer = new PaddleChart2TableTokenizer(root!);
            GenerativeTokenSequence sequence = tokenizer.EncodeChartPrompt();
            long[] ids = sequence.CopyTokenIds();
            Assert.AreEqual(286, ids.Length);
            Assert.AreEqual(151644L, ids[0]);
            Assert.AreEqual(151645L, ids[16]);
            Assert.AreEqual(151859L, ids[21]);
            Assert.AreEqual(151858L, ids[277]);
            Assert.AreEqual(256, ids.Count(value => value == 151859L));
            Assert.IsTrue(ids.Contains(151858L));
            Assert.IsTrue(ids.All(value => value >= 0 && value < tokenizer.VocabularySize));
            Assert.AreEqual("paddle-chart2table-qwen-tiktoken", tokenizer.TokenizerId);
            Assert.IsTrue(tokenizer.IsTerminalToken(151643));
            Assert.IsTrue(tokenizer.IsTerminalToken(151645));
            Assert.IsFalse(tokenizer.IsTerminalToken(151644));
            Assert.AreEqual("年份 |", tokenizer.DecodeCompletion(new[] { 7948, 69442, 760 }));
            Assert.AreEqual(string.Empty, tokenizer.DecodeCompletion(new[] { 151643, 151645 }));
            string tokenSha;
            using (SHA256 sha = SHA256.Create()) tokenSha = string.Concat(sha.ComputeHash(Encoding.ASCII.GetBytes(string.Join(",", ids))).Select(value => value.ToString("x2")));
            Assert.AreEqual("6097fbd88b37550710793448045e068bb91f389cce3c10a27916a6bb0d3a2249", tokenSha);
        }
    }
}
