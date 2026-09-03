using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Tensors;
using JYPPX.DeploySharp.Visual;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploySharp.Visual.Tests
{
    [TestClass]
    public sealed class AudioUnderstandingTests
    {
        private static readonly BackendId Backend = new BackendId("audio-fake");

        [TestMethod]
        public void ProfilesBindExecutableCtcAndPreserveExactSourceOnlyBoundaries()
        {
            AudioUnderstandingProfile onnx = AudioUnderstandingProfiles.CreateWav2Vec2Base960hOnnx(); AudioUnderstandingProfile openVino = AudioUnderstandingProfiles.CreateWav2Vec2Base960hOpenVino();
            Assert.IsTrue(onnx.Executable); Assert.AreEqual(AudioUnderstandingFamily.Wav2Vec2, onnx.Family); Assert.AreEqual(16000, onnx.Processor.SampleRate); Assert.AreEqual(320, onnx.Timestamps.FrameStrideSamples); Assert.AreEqual(32, onnx.Tokenizer!.VocabularySize); Assert.AreEqual("input_values", onnx.GetArtifact(AudioArtifactRole.CtcEncoderHead).Inputs[0].Name); Assert.AreEqual("logits", onnx.GetArtifact(AudioArtifactRole.CtcEncoderHead).Outputs[0].Name);
            Assert.AreEqual("openvino-ir", openVino.GetArtifact(AudioArtifactRole.CtcEncoderHead).Format); Assert.AreEqual("b5f086a228f79416658ff7d4ac2ab897183e3719ba994453506b8aef408ec803", openVino.GetArtifact(AudioArtifactRole.CtcEncoderHead).SidecarSha256);
            AudioUnderstandingProfile whisper = AudioUnderstandingProfiles.CreateWhisperTinyEnglishContract(); Assert.IsFalse(whisper.Executable); Assert.AreEqual(50362, whisper.Generation!.NoTimestampsTokenId); Assert.IsNull(whisper.Generation.LanguageTokenId); Assert.AreEqual(4, whisper.Generation.KvLayers); Assert.AreEqual(3, whisper.Blocker!.MissingRoles.Count); Assert.AreEqual("stage28-whisper-source-only-graph-bundle-pending", whisper.Blocker.BlockerId); StringAssert.Contains(whisper.Blocker.Reproduction, "WhisperUnderstandingSession");
            AudioUnderstandingProfile whisperOnnx = AudioUnderstandingProfiles.CreateWhisperTinyEnglishOnnx(); Assert.IsTrue(whisperOnnx.Executable); Assert.AreEqual(3, whisperOnnx.Artifacts.Count); Assert.AreEqual("input_features", whisperOnnx.GetArtifact(AudioArtifactRole.WhisperEncoder).Inputs[0].Name); Assert.AreEqual("present.3.encoder.value", whisperOnnx.GetArtifact(AudioArtifactRole.WhisperDecoderWithPast).Outputs[16].Name);
            AudioUnderstandingProfile hubert = AudioUnderstandingProfiles.CreateHubertBaseLs960Contract(); Assert.IsFalse(hubert.Executable); Assert.IsTrue(hubert.Tasks.Contains(AudioUnderstandingTask.SpeechRepresentation)); Assert.IsFalse(hubert.Tasks.Contains(AudioUnderstandingTask.AutomaticSpeechRecognition));
            AudioUnderstandingProfile pyannote = AudioUnderstandingProfiles.CreatePyannoteSpeakerDiarization31Contract(); Assert.IsFalse(pyannote.Executable); Assert.AreEqual(AudioSpeakerOwnership.ModelPipeline, pyannote.Speaker.Ownership); Assert.IsTrue(pyannote.Speaker.OwnsVad && pyannote.Speaker.OwnsEmbeddings && pyannote.Speaker.OwnsClustering && pyannote.Speaker.OwnsLabels);
            Assert.AreEqual(VisualErrorCodes.AudioCapabilityUnavailable, Assert.ThrowsExactly<VisualException>(() => new AudioUnderstandingBundle(whisper, Array.Empty<AudioArtifactBinding>())).ErrorCode);
        }

        [TestMethod]
        public void WhisperTokenizerMatchesPinnedPromptAndRoundTripsEnglishTextWhenCheckpointIsPresent()
        {
#if NET8_0 || NET9_0 || NET10_0
            string checkpoint = Environment.GetEnvironmentVariable("DEPLOYSHARP_WHISPER_CHECKPOINT") ?? @"E:\DeploySharp-Models\whisper-tiny.en\checkpoint";
            if (!System.IO.Directory.Exists(checkpoint)) Assert.Inconclusive("The pinned Whisper checkpoint is not available: " + checkpoint);
            AudioUnderstandingProfile profile = AudioUnderstandingProfiles.CreateWhisperTinyEnglishContract();
            WhisperTokenizer tokenizer = new WhisperTokenizer(checkpoint, profile.Generation!);
            WhisperTokenSequence prompt = tokenizer.EncodePrompt(profile);
            CollectionAssert.AreEqual(new long[] { 50257, 50362 }, prompt.TokenIds.ToArray());
            IReadOnlyList<int> encoded = tokenizer.EncodeText("hello world");
            Assert.AreEqual("hello world", tokenizer.DecodeText(encoded));
            Assert.AreEqual(string.Empty, tokenizer.DecodeText(new[] { 50256, 50257, 50362, 50363 }));
#else
            Assert.Inconclusive("The managed Whisper tokenizer requires net8.0 or later.");
#endif
        }

        [TestMethod]
        public void PreparedWhisperInputEnforcesFeatureShapeAndFiniteValues()
        {
            AudioUnderstandingProfile profile = AudioUnderstandingProfiles.CreateWhisperTinyEnglishOnnx();
            using var input = new PreparedWhisperInput(profile, "input_features", new Tensor<float>(new TensorShape(1, 80, 3000), new float[240000], TensorBufferOwnership.Transfer), "unit-whisper", new string('1', 64), new string('2', 64), TimeSpan.Zero);
            Assert.AreEqual("input_features", input.InputName); Assert.AreEqual(240000, input.Tensor.Length); Assert.IsFalse(input.IsDisposed);
            Assert.AreEqual(VisualErrorCodes.AudioNonFinite, Assert.ThrowsExactly<VisualException>(() => new PreparedWhisperInput(profile, "input_features", new Tensor<float>(new TensorShape(1, 80, 3000), Enumerable.Repeat(float.NaN, 240000).ToArray(), TensorBufferOwnership.Transfer), "unit-whisper", new string('1', 64), new string('2', 64), TimeSpan.Zero)).ErrorCode);
        }

        [TestMethod]
        public void WhisperLogMelExtractorProducesDeterministicFixedShapeFeaturesWhenCheckpointIsPresent()
        {
#if NET8_0 || NET9_0 || NET10_0
            string checkpoint = Environment.GetEnvironmentVariable("DEPLOYSHARP_WHISPER_CHECKPOINT") ?? @"E:\DeploySharp-Models\whisper-tiny.en\checkpoint";
            if (!System.IO.Directory.Exists(checkpoint)) Assert.Inconclusive("The pinned Whisper checkpoint is not available: " + checkpoint);
            AudioUnderstandingProfile profile = AudioUnderstandingProfiles.CreateWhisperTinyEnglishOnnx();
            var extractor = new WhisperLogMelExtractor(checkpoint, profile.Processor);
            var samples = new float[16000]; samples[4000] = 1f;
            var watch = System.Diagnostics.Stopwatch.StartNew(); Tensor<float> first = extractor.Extract(samples); watch.Stop(); Tensor<float> second = extractor.Extract(samples);
            Assert.AreEqual(new TensorShape(1, 80, 3000), first.Shape); Assert.AreEqual(240000, first.Length); CollectionAssert.AreEqual((float[])first.Buffer, (float[])second.Buffer); float[] values = (float[])first.Buffer; Assert.IsTrue(values.All(value => !float.IsNaN(value) && !float.IsInfinity(value))); Assert.AreEqual(-1.3922093f, values.Min(), 0.000001f); Assert.AreEqual(0.6077907f, values.Max(), 0.000001f); Assert.AreEqual(108025, Array.IndexOf(values, values.Max())); Console.WriteLine("STAGE28_WHISPER_LOGMEL_FIRST=" + string.Join(",", values.Take(12).Select(value => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)))); Console.WriteLine("STAGE28_WHISPER_LOGMEL ms=" + watch.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + ";shape=" + string.Join("x", first.Shape.ToArray()));
#else
            Assert.Inconclusive("The managed Whisper log-Mel extractor requires net8.0 or later.");
#endif
        }

        [TestMethod]
        public async Task WhisperLogMelExtractorIsDeterministicAcrossConcurrentCallsWhenCheckpointIsPresent()
        {
#if NET8_0 || NET9_0 || NET10_0
            string checkpoint = Environment.GetEnvironmentVariable("DEPLOYSHARP_WHISPER_CHECKPOINT") ?? @"E:\DeploySharp-Models\whisper-tiny.en\checkpoint";
            if (!System.IO.Directory.Exists(checkpoint)) Assert.Inconclusive("The pinned Whisper checkpoint is not available: " + checkpoint);
            AudioUnderstandingProfile profile = AudioUnderstandingProfiles.CreateWhisperTinyEnglishOnnx();
            var extractor = new WhisperLogMelExtractor(checkpoint, profile.Processor);
            var samples = new float[16000]; samples[4000] = 1f;
            Tensor<float> baseline = extractor.Extract(samples);
            Tensor<float>[] concurrent = await Task.WhenAll(
                Task.Run(() => extractor.Extract(samples)),
                Task.Run(() => extractor.Extract(samples)),
                Task.Run(() => extractor.Extract(samples)),
                Task.Run(() => extractor.Extract(samples)));
            for (int index = 0; index < concurrent.Length; index++) CollectionAssert.AreEqual((float[])baseline.Buffer, (float[])concurrent[index].Buffer);
#else
            Assert.Inconclusive("The managed Whisper log-Mel extractor requires net8.0 or later.");
#endif
        }

        [TestMethod]
        public void CtcDecoderUsesBlankResetRepeatCollapseLowestTieAndFrameSpans()
        {
            AudioUnderstandingProfile profile = AudioUnderstandingProfiles.CreateWav2Vec2Base960hOnnx(); Wav2Vec2CtcVocabulary vocabulary = Vocabulary(profile.Tokenizer!); var decoder = new AudioCtcDecoder(vocabulary, profile.Timestamps);
            int[] selected = { 0, 19, 19, 0, 7, 5, 6, 6, 0 }; float[] values = Enumerable.Repeat(-10f, selected.Length * 32).ToArray();
            for (int frame = 0; frame < selected.Length; frame++) values[(frame * 32) + selected[frame]] = 10f;
            values[(5 * 32) + 6] = 10f; // E and T tie; E has the lower ID.
            AudioCtcDecodedResult result = decoder.Decode(new Tensor<float>(new TensorShape(1, selected.Length, 32), values));
            Assert.AreEqual("CAET", result.Transcript); CollectionAssert.AreEqual(selected, result.FrameTokenIds.ToArray()); CollectionAssert.AreEqual(new[] { 19, 7, 5, 6 }, result.CollapsedTokenIds.ToArray()); Assert.AreEqual(4, result.Segments.Count); Assert.AreEqual(1, result.Segments[0].StartFrame); Assert.AreEqual(3, result.Segments[0].EndFrameExclusive); Assert.AreEqual(TimeSpan.FromSeconds(.02), result.Segments[0].Start); Assert.AreEqual(TimeSpan.FromSeconds(.06), result.Segments[0].End); Assert.IsTrue(result.Segments[0].MeanSelectedProbability > .99f); Assert.IsTrue(result.Segments[2].MeanSelectedProbability > .49f && result.Segments[2].MeanSelectedProbability < .51f);
            values[0] = float.NaN; Assert.AreEqual(VisualErrorCodes.AudioNonFinite, Assert.ThrowsExactly<VisualException>(() => decoder.Decode(new Tensor<float>(new TensorShape(1, selected.Length, 32), values))).ErrorCode);
            Assert.AreEqual(VisualErrorCodes.AudioCtcDecodeInvalid, Assert.ThrowsExactly<VisualException>(() => decoder.Decode(new Tensor<float>(new TensorShape(1, 1, 31), new float[31]))).ErrorCode);
        }

        [TestMethod]
        public void CtcDecoderSkipsTimestampWorkWhenTimestampsAreDisabled()
        {
            AudioUnderstandingProfile profile = AudioUnderstandingProfiles.CreateWav2Vec2Base960hOnnx(); Wav2Vec2CtcVocabulary vocabulary = Vocabulary(profile.Tokenizer!); var decoder = new AudioCtcDecoder(vocabulary, profile.Timestamps);
            int[] selected = { 0, 19, 19, 0, 7, 5, 6, 6, 0 }; float[] values = Enumerable.Repeat(-10f, selected.Length * 32).ToArray();
            for (int frame = 0; frame < selected.Length; frame++) values[(frame * 32) + selected[frame]] = 10f;
            AudioCtcDecodedResult result = decoder.Decode(new Tensor<float>(new TensorShape(1, selected.Length, 32), values), includeTokenTimestamps: false);
            Assert.AreEqual("CAET", result.Transcript); CollectionAssert.AreEqual(new[] { 19, 7, 5, 6 }, result.CollapsedTokenIds.ToArray()); Assert.AreEqual(0, result.Segments.Count);
        }

        [TestMethod]
        public async Task SessionOwnsAtomicStateAndRejectsMismatchConcurrencyCancellationAndDisposedUse()
        {
            using Fixture fixture = Fixture.Create(TimeSpan.Zero); using PreparedAudioInput input = fixture.Input(); AudioStateSummary state = fixture.Session.SetAudio(input); Assert.AreEqual(8, state.SampleCount); Assert.IsTrue(fixture.Session.HasAudio);
            AudioTranscriptionResult result = fixture.Session.Transcribe(new AudioTranscriptionRequest(AudioUnderstandingTask.CtcTranscription, "en")); Assert.AreEqual("CAT", result.Decoded.Transcript); Assert.AreEqual(3, result.Decoded.Segments.Count); Assert.AreEqual("success", result.ParseStatus); Assert.IsTrue(result.Timing.Inference >= TimeSpan.Zero);
            Assert.AreEqual(VisualErrorCodes.AudioCapabilityUnavailable, Assert.ThrowsExactly<VisualException>(() => fixture.Session.Transcribe(new AudioTranscriptionRequest(AudioUnderstandingTask.CtcTranscription, "fr"))).ErrorCode);
            fixture.Provider.NaNLogits = true; Assert.AreEqual(VisualErrorCodes.AudioNonFinite, Assert.ThrowsExactly<VisualException>(() => fixture.Session.Transcribe(new AudioTranscriptionRequest(AudioUnderstandingTask.CtcTranscription, "en"))).ErrorCode); fixture.Provider.NaNLogits = false;
            fixture.Session.Reset(); Assert.IsFalse(fixture.Session.HasAudio); Assert.AreEqual(VisualErrorCodes.AudioStateInvalid, Assert.ThrowsExactly<VisualException>(() => fixture.Session.Transcribe(new AudioTranscriptionRequest(AudioUnderstandingTask.CtcTranscription, "en"))).ErrorCode); fixture.Session.SetAudio(input);

            using Fixture delayed = Fixture.Create(TimeSpan.FromMilliseconds(150)); using PreparedAudioInput delayedInput = delayed.Input(); delayed.Session.SetAudio(delayedInput); Task<AudioTranscriptionResult> active = delayed.Session.TranscribeAsync(new AudioTranscriptionRequest(AudioUnderstandingTask.CtcTranscription, "en")); await Task.Delay(20); Assert.AreEqual(VisualErrorCodes.AudioConcurrentOperation, Assert.ThrowsExactly<VisualException>(() => delayed.Session.Clear()).ErrorCode); await active;
            using (var cancellation = new CancellationTokenSource(20)) { VisualException cancelled = await Assert.ThrowsExactlyAsync<VisualException>(() => delayed.Session.TranscribeAsync(new AudioTranscriptionRequest(AudioUnderstandingTask.CtcTranscription, "en"), cancellationToken: cancellation.Token)); Assert.AreEqual(VisualErrorCodes.AudioCancelled, cancelled.ErrorCode); }
            delayed.Session.Dispose(); Assert.AreEqual(VisualErrorCodes.AudioDisposed, Assert.ThrowsExactly<VisualException>(() => delayed.Session.Clear()).ErrorCode);
        }

        private static Wav2Vec2CtcVocabulary Vocabulary(AudioTokenizerContract contract)
        {
            string[] tokens = { "<pad>", "<s>", "</s>", "<unk>", "|", "E", "T", "A", "O", "N", "I", "H", "S", "R", "D", "L", "U", "M", "W", "C", "F", "G", "Y", "P", "B", "V", "K", "'", "X", "J", "Q", "Z" };
            return new Wav2Vec2CtcVocabulary(contract, tokens.Select((value, index) => new KeyValuePair<int, string>(index, value)).ToDictionary(value => value.Key, value => value.Value));
        }

        private sealed class Fixture : IDisposable
        {
            private Fixture(AudioUnderstandingProfile profile, Provider provider, BackendRegistry registry, AudioUnderstandingSession session) { Profile = profile; Provider = provider; Registry = registry; Session = session; }
            internal AudioUnderstandingProfile Profile { get; } internal Provider Provider { get; } internal BackendRegistry Registry { get; } internal AudioUnderstandingSession Session { get; }
            internal static Fixture Create(TimeSpan delay)
            {
                AudioUnderstandingProfile profile = AudioUnderstandingProfiles.CreateWav2Vec2Base960hOnnx(); var provider = new Provider(profile, delay); var registry = new BackendRegistry(); registry.Register(provider); AudioArtifactContract contract = profile.GetArtifact(AudioArtifactRole.CtcEncoderHead); var bundle = new AudioUnderstandingBundle(profile, new[] { new AudioArtifactBinding(AudioArtifactRole.CtcEncoderHead, contract.CreateArtifact("fake-audio.onnx", Backend)) });
                return new Fixture(profile, provider, registry, new AudioUnderstandingSession(registry, bundle, Vocabulary(profile.Tokenizer!), new BackendRequest(BackendCapabilities.TensorInference, Backend, "cpu")));
            }
            internal PreparedAudioInput Input()
            {
                var tensor = new Tensor<float>(new TensorShape(1, 8), new float[] { -.5f, -.25f, 0, .25f, .5f, .25f, 0, -.25f }); var source = new AudioSourceDescriptor("unit", new string('1', 64), 16, 16000, 1, 8, AudioPcmEncoding.SignedInt16LittleEndian, AudioChannelLayout.Mono, "unit-test-generated");
                return new PreparedAudioInput(Profile, "input_values", tensor, source, new string('2', 64), new string('3', 64), TimeSpan.Zero);
            }
            public void Dispose() { Session.Dispose(); Registry.Dispose(); }
        }

        private sealed class Provider : IBackendProvider
        {
            private readonly AudioUnderstandingProfile _profile; private readonly TimeSpan _delay; internal Provider(AudioUnderstandingProfile profile, TimeSpan delay) { _profile = profile; _delay = delay; Descriptor = new BackendDescriptor(Backend, "Audio fake", "1", BackendCapabilities.TensorInference | BackendCapabilities.AsynchronousExecution | BackendCapabilities.DynamicShapes, new[] { "onnx" }); }
            public BackendDescriptor Descriptor { get; } internal bool NaNLogits { get; set; }
            public bool CanCreate(ModelArtifact artifact, BackendRequest request) => _profile.Artifacts.Any(value => value.ModelId == artifact.ModelId) && Descriptor.Supports(request.RequiredCapabilities);
            public IInferenceSession CreateSession(ModelArtifact artifact, BackendRequest request, SessionOptions options)
            {
                AudioArtifactContract contract = _profile.GetArtifact(AudioArtifactRole.CtcEncoderHead); var metadata = new ModelMetadata(contract.ModelId, contract.Format, contract.Inputs.Select(value => new TensorDescriptor(value.Name, value.ElementType, value.ShapePattern)), contract.Outputs.Select(value => new TensorDescriptor(value.Name, value.ElementType, value.ShapePattern)));
                return new FakeSession(metadata, _delay, this);
            }
            public void Dispose() { }
        }

        private sealed class FakeSession : IInferenceSession
        {
            private readonly TimeSpan _delay; private readonly Provider _provider; private bool _disposed; internal FakeSession(ModelMetadata metadata, TimeSpan delay, Provider provider) { Metadata = metadata; _delay = delay; _provider = provider; }
            public ModelMetadata Metadata { get; }
            public InferenceOutputs Run(InferenceInputs inputs, CancellationToken cancellationToken) => RunAsync(inputs, cancellationToken).GetAwaiter().GetResult();
            public async Task<InferenceOutputs> RunAsync(InferenceInputs inputs, CancellationToken cancellationToken)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(FakeSession)); if (_delay > TimeSpan.Zero) await Task.Delay(_delay, cancellationToken); int[] tokens = { 0, 19, 19, 0, 7, 6, 6, 0 }; float[] values = Enumerable.Repeat(-10f, tokens.Length * 32).ToArray(); for (int frame = 0; frame < tokens.Length; frame++) values[(frame * 32) + tokens[frame]] = 10f; if (_provider.NaNLogits) values[0] = float.NaN; return InferenceOutputs.Create("logits", new Tensor<float>(new TensorShape(1, tokens.Length, 32), values));
            }
            public void Dispose() { _disposed = true; }
        }
    }
}
