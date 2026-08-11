using System;
using System.IO;
using JYPPX.DeploySharp;
using JYPPX.DeploySharp.Backends.OnnxRuntime;
using JYPPX.DeploySharp.Models;
using JYPPX.DeploySharp.Registry;
using JYPPX.DeploySharp.Visual;
using JYPPX.DeploySharp.Visual.OpenCV;

internal static class Program
{
    private const string ExpectedTranscript = "CONCORD RETURNED TO ITS PLACE AMIDST THE TENTS";

    private static int Main()
    {
        string? root = Environment.GetEnvironmentVariable("DEPLOYSHARP_AUDIO_MODEL_ROOT");
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            Console.WriteLine("DEPLOYSHARP_AUDIO_SPEECH_FAMILY_CONSUMER_SKIP missing-external-file");
            return 0;
        }

        string wav = Path.Combine(root, "dataset", "6930-75918-0000.wav");
        string model = Path.Combine(root, "onnx", "wav2vec2-base-960h-ctc.onnx");
        string vocab = Path.Combine(root, "checkpoint", "vocab.json");
        if (!File.Exists(wav) || !File.Exists(model) || !File.Exists(vocab))
        {
            Console.WriteLine("DEPLOYSHARP_AUDIO_SPEECH_FAMILY_CONSUMER_SKIP missing-external-file");
            return 0;
        }

        AudioUnderstandingProfile profile = AudioUnderstandingProfiles.CreateWav2Vec2Base960hOnnx();
        BackendId backend = OnnxRuntimeBackendProvider.BackendId;
        AudioArtifactContract contract = profile.GetArtifact(AudioArtifactRole.CtcEncoderHead);
        var bundle = new AudioUnderstandingBundle(profile, new[] { new AudioArtifactBinding(AudioArtifactRole.CtcEncoderHead, contract.CreateArtifact(model, backend)) });
        using var registry = new BackendRegistry();
        registry.UseOnnxRuntime();
        var tokenizer = Wav2Vec2CtcVocabulary.Load(vocab, profile.Tokenizer!);
        using var session = new AudioUnderstandingSession(registry, bundle, tokenizer, new BackendRequest(BackendCapabilities.TensorInference, backend, "cpu"));
        using PreparedAudioInput input = new OpenCvAudioInputFactory().CreateFromWavFile(wav, profile, "LibriSpeech CC-BY-4.0 row 6930-75918-0000", "openslr/librispeech_asr:clean/test:0");
        AudioStateSummary state = session.SetAudio(input);
        AudioTranscriptionResult result = session.Transcribe(new AudioTranscriptionRequest(AudioUnderstandingTask.CtcTranscription, "en", true, "clean-consumer"));
        if (!string.Equals(result.Decoded.Transcript, ExpectedTranscript, StringComparison.Ordinal)) throw new InvalidOperationException("The external Wav2Vec2 transcript did not match the audited result.");
        session.Clear();
        try { session.Transcribe(new AudioTranscriptionRequest(AudioUnderstandingTask.CtcTranscription, "en")); throw new InvalidOperationException("Clear did not invalidate audio state."); }
        catch (VisualException exception) when (exception.ErrorCode == VisualErrorCodes.AudioStateInvalid) { }
        Console.WriteLine("audio-family:state=" + state.StateIdentity + ";feature=" + state.FeatureSha256 + ";frames=" + result.Decoded.FrameTokenIds.Count + ";transcript=" + result.Decoded.Transcript);
        Console.WriteLine("DEPLOYSHARP_AUDIO_SPEECH_FAMILY_CONSUMER_OK");
        return 0;
    }
}
