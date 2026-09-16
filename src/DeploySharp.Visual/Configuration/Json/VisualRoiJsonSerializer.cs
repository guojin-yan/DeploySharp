using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using JYPPX.DeploySharp.Geometry;

namespace JYPPX.DeploySharp.Visual.Configuration.Json
{
    /// <summary>Serializes and validates versioned Visual ROI snapshots. / 序列化并验证版本化 Visual ROI 快照。</summary>
    public static class VisualRoiJsonSerializer
    {
        private const string CurrentSchemaVersion = "1.0";

        /// <summary>Serializes a snapshot using stable property and collection ordering. / 使用稳定字段和集合顺序序列化快照。</summary>
        public static string Serialize(VisualRoiSnapshot snapshot, VisualRoiJsonOptions? options = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            VisualRoiJsonOptions effective = options ?? VisualRoiJsonOptions.Default;
            if (snapshot.Rois.Count > effective.MaximumRois) throw Error("limit_exceeded", "$..rois", "The ROI count exceeds the configured JSON limit.");
            using (var stream = new MemoryStream())
            {
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, SkipValidation = false })) WriteDocument(writer, snapshot, effective);
                if (stream.Length > effective.MaximumDocumentBytes) throw Error("limit_exceeded", "$", "The serialized ROI document exceeds the configured byte limit.");
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>Writes a snapshot to a stream. / 将快照写入流。</summary>
        public static void Serialize(Stream stream, VisualRoiSnapshot snapshot, VisualRoiJsonOptions? options = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite) throw new ArgumentException("The target stream must be writable.", nameof(stream));
            byte[] bytes = Encoding.UTF8.GetBytes(Serialize(snapshot, options));
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>Deserializes a bounded ROI document and returns its immutable snapshot. / 反序列化有界 ROI 文档并返回不可变快照。</summary>
        public static VisualRoiSnapshot Deserialize(string json, VisualRoiJsonOptions? options = null)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            VisualRoiJsonOptions effective = options ?? VisualRoiJsonOptions.Default;
            if (Encoding.UTF8.GetByteCount(json) > effective.MaximumDocumentBytes) throw Error("limit_exceeded", "$", "The ROI document exceeds the configured byte limit.");
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 64 }))
                {
                    return ReadDocument(document.RootElement, effective);
                }
            }
            catch (VisualRoiJsonException) { throw; }
            catch (JsonException exception) { throw Error("invalid_json", "$", "The ROI document is not valid JSON.", exception); }
            catch (FormatException exception) { throw Error("invalid_value", "$", "The ROI document contains an invalid encoded value.", exception); }
            catch (OverflowException exception) { throw Error("invalid_value", "$", "The ROI document contains an overflowing numeric value.", exception); }
        }

        /// <summary>Reads a UTF-8 JSON document from a stream. / 从流读取 UTF-8 JSON 文档。</summary>
        public static VisualRoiSnapshot Deserialize(Stream stream, VisualRoiJsonOptions? options = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("The source stream must be readable.", nameof(stream));
            VisualRoiJsonOptions effective = options ?? VisualRoiJsonOptions.Default;
            using (var buffer = new MemoryStream())
            {
                var chunk = new byte[81920];
                int read;
                while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    if (buffer.Length + read > effective.MaximumDocumentBytes) throw Error("limit_exceeded", "$", "The ROI document exceeds the configured byte limit.");
                    buffer.Write(chunk, 0, read);
                }
                return Deserialize(Encoding.UTF8.GetString(buffer.ToArray()), effective);
            }
        }

        /// <summary>Parses JSON and atomically installs it as the next manager snapshot. / 解析 JSON 并原子安装为管理器的下一版快照。</summary>
        /// <remarks>The manager is changed only after the complete document has passed all limits and geometry validation. The serialized version is treated as file metadata; the manager assigns its own monotonic version. / 只有文档完整通过限制和几何校验后才会修改管理器；序列化版本作为文件元数据，管理器会分配自己的单调版本。</remarks>
        public static VisualRoiSnapshot LoadAndReplace(VisualRoiManager manager, string json, VisualRoiJsonOptions? options = null)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            VisualRoiSnapshot candidate = Deserialize(json, options);
            return manager.Replace(candidate.SourceSize, candidate.Rois);
        }

        /// <summary>Loads a file and atomically installs the validated ROI snapshot. / 加载文件并原子安装通过校验的 ROI 快照。</summary>
        public static VisualRoiSnapshot LoadAndReplaceFile(VisualRoiManager manager, string path, VisualRoiJsonOptions? options = null)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A ROI configuration path is required.", nameof(path));
            string fullPath;
            try { fullPath = Path.GetFullPath(path); }
            catch (Exception exception) { throw Error("invalid_path", "$.file", "The ROI configuration path is invalid.", exception); }
            try
            {
                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read)) return LoadAndReplace(manager, stream, options);
            }
            catch (VisualRoiJsonException) { throw; }
            catch (Exception exception) { throw Error("io_error", "$.file", "The ROI configuration file could not be read.", exception); }
        }

        /// <summary>Loads a stream and atomically installs the validated ROI snapshot. / 加载流并原子安装通过校验的 ROI 快照。</summary>
        public static VisualRoiSnapshot LoadAndReplace(VisualRoiManager manager, Stream stream, VisualRoiJsonOptions? options = null)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            VisualRoiSnapshot candidate = Deserialize(stream, options);
            return manager.Replace(candidate.SourceSize, candidate.Rois);
        }

        private static void WriteDocument(Utf8JsonWriter writer, VisualRoiSnapshot snapshot, VisualRoiJsonOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", CurrentSchemaVersion);
            writer.WriteStartObject("source");
            writer.WriteNumber("width", snapshot.SourceSize.Width);
            writer.WriteNumber("height", snapshot.SourceSize.Height);
            writer.WriteEndObject();
            writer.WriteNumber("version", snapshot.Version);
            writer.WriteStartArray("rois");
            foreach (VisualRoi roi in snapshot.Rois) WriteRoi(writer, roi, options);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteRoi(Utf8JsonWriter writer, VisualRoi roi, VisualRoiJsonOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("id", roi.Id);
            writer.WriteString("name", roi.Name);
            writer.WriteBoolean("enabled", roi.Enabled);
            writer.WriteNumber("priority", roi.Priority);
            writer.WriteString("coordinateSpace", roi.CoordinateSpace.ToString());
            writer.WriteString("inclusionMode", roi.InclusionMode.ToString());
            writer.WriteString("executionMode", roi.ExecutionMode.ToString());
            writer.WriteString("hitTestMode", roi.HitTestMode.ToString());
            writer.WriteNumber("hitThreshold", roi.HitThreshold);
            writer.WriteNumber("margin", roi.Margin);
            if (roi.ConfidenceOverride.HasValue) writer.WriteNumber("confidenceOverride", roi.ConfidenceOverride.Value);
            writer.WriteStartArray("taskFilter");
            foreach (VisualTaskId task in roi.TaskFilter.OrderBy(value => value.Value, StringComparer.Ordinal)) writer.WriteStringValue(task.Value);
            writer.WriteEndArray();
            writer.WriteStartArray("classFilter");
            foreach (int value in roi.ClassFilter.OrderBy(value => value)) writer.WriteNumberValue(value);
            writer.WriteEndArray();
            writer.WriteStartArray("tags");
            foreach (string value in roi.Tags.OrderBy(value => value, StringComparer.Ordinal)) writer.WriteStringValue(value);
            writer.WriteEndArray();
            writer.WriteStartObject("metadata");
            foreach (KeyValuePair<string, string> value in roi.Metadata.OrderBy(value => value.Key, StringComparer.Ordinal)) writer.WriteString(value.Key, value.Value);
            writer.WriteEndObject();
            WriteGeometry(writer, roi.Geometry);
            writer.WriteEndObject();
        }

        private static void WriteGeometry(Utf8JsonWriter writer, IVisualRoiGeometry geometry)
        {
            writer.WriteStartObject("geometry");
            writer.WriteString("type", geometry.Kind.ToString());
            if (geometry is RectangleRoiGeometry rectangle)
            {
                WriteRectangle(writer, rectangle.Rectangle);
            }
            else if (geometry is RotatedRectangleRoiGeometry rotated)
            {
                writer.WriteNumber("centerX", rotated.Center.X);
                writer.WriteNumber("centerY", rotated.Center.Y);
                writer.WriteNumber("width", rotated.Size.Width);
                writer.WriteNumber("height", rotated.Size.Height);
                writer.WriteNumber("angleDegrees", rotated.AngleDegrees);
            }
            else if (geometry is PolygonRoiGeometry polygon)
            {
                WritePoints(writer, polygon.Points);
            }
            else if (geometry is MaskRoiGeometry mask)
            {
                // A SourcePixels mask must match the document source. TileLocal and
                // World masks intentionally retain their native raster dimensions;
                // the execution context maps that raster into source coordinates.
                writer.WriteNumber("width", mask.SourceSize.Width);
                writer.WriteNumber("height", mask.SourceSize.Height);
                writer.WriteString("valuesBase64", Convert.ToBase64String(mask.ToArray()));
            }
            else throw Error("unsupported_geometry", "$.geometry", "The ROI geometry type is not supported by the JSON serializer.");
            writer.WriteEndObject();
        }

        private static void WriteRectangle(Utf8JsonWriter writer, RectangleF rectangle)
        {
            writer.WriteNumber("x", rectangle.X);
            writer.WriteNumber("y", rectangle.Y);
            writer.WriteNumber("width", rectangle.Width);
            writer.WriteNumber("height", rectangle.Height);
        }

        private static void WritePoints(Utf8JsonWriter writer, IReadOnlyList<PointF> points)
        {
            writer.WriteStartArray("points");
            foreach (PointF point in points)
            {
                writer.WriteStartObject();
                writer.WriteNumber("x", point.X);
                writer.WriteNumber("y", point.Y);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        private static VisualRoiSnapshot ReadDocument(JsonElement root, VisualRoiJsonOptions options)
        {
            EnsureObject(root, "$", options);
            EnsureProperties(root, "$", options, "schemaVersion", "source", "version", "rois");
            string schema = RequiredString(root, "schemaVersion", "$.schemaVersion");
            if (!string.Equals(schema, CurrentSchemaVersion, StringComparison.Ordinal)) throw Error("unsupported_schema", "$.schemaVersion", "Only schema version 1.0 is supported.");
            JsonElement source = RequiredObject(root, "source", "$.source", options);
            EnsureProperties(source, "$.source", options, "width", "height");
            VisualSize sourceSize = new VisualSize(RequiredPositiveInt(source, "width", "$.source.width"), RequiredPositiveInt(source, "height", "$.source.height"));
            long version = RequiredPositiveLong(root, "version", "$.version");
            JsonElement rois = RequiredArray(root, "rois", "$.rois");
            if (rois.GetArrayLength() > options.MaximumRois) throw Error("limit_exceeded", "$.rois", "The ROI count exceeds the configured JSON limit.");
            var values = new List<VisualRoi>(rois.GetArrayLength());
            int index = 0;
            foreach (JsonElement element in rois.EnumerateArray()) values.Add(ReadRoi(element, "$.rois[" + index++ + "]", sourceSize, options));
            try { return new VisualRoiSnapshot(sourceSize, values, version); }
            catch (Exception exception) when (exception is ArgumentException || exception is ArgumentOutOfRangeException)
            { throw Error("invalid_roi", "$.rois", "The ROI collection violates a geometry or uniqueness constraint.", exception); }
        }

        private static VisualRoi ReadRoi(JsonElement element, string path, VisualSize sourceSize, VisualRoiJsonOptions options)
        {
            EnsureObject(element, path, options);
            EnsureProperties(element, path, options, "id", "name", "enabled", "priority", "coordinateSpace", "inclusionMode", "executionMode", "hitTestMode", "hitThreshold", "margin", "confidenceOverride", "taskFilter", "classFilter", "tags", "metadata", "geometry");
            string id = RequiredString(element, "id", path + ".id");
            string name = RequiredString(element, "name", path + ".name");
            bool enabled = RequiredBool(element, "enabled", path + ".enabled");
            int priority = RequiredInt(element, "priority", path + ".priority");
            RoiCoordinateSpace coordinateSpace = ParseEnum<RoiCoordinateSpace>(element, "coordinateSpace", path + ".coordinateSpace");
            RoiInclusionMode inclusion = ParseEnum<RoiInclusionMode>(element, "inclusionMode", path + ".inclusionMode");
            RoiExecutionMode execution = ParseEnum<RoiExecutionMode>(element, "executionMode", path + ".executionMode");
            RoiHitTestMode hitTest = ParseEnum<RoiHitTestMode>(element, "hitTestMode", path + ".hitTestMode");
            float threshold = RequiredFloat(element, "hitThreshold", path + ".hitThreshold");
            float margin = RequiredFloat(element, "margin", path + ".margin");
            float? confidenceOverride = null;
            if (element.TryGetProperty("confidenceOverride", out JsonElement confidenceElement)) confidenceOverride = RequiredFloat(confidenceElement, path + ".confidenceOverride");
            var tasks = new List<VisualTaskId>();
            JsonElement taskFilter = RequiredArray(element, "taskFilter", path + ".taskFilter");
            foreach (JsonElement task in taskFilter.EnumerateArray()) tasks.Add(new VisualTaskId(RequiredString(task, path + ".taskFilter")));
            var classes = new List<int>();
            JsonElement classFilter = RequiredArray(element, "classFilter", path + ".classFilter");
            foreach (JsonElement value in classFilter.EnumerateArray()) classes.Add(RequiredInt(value, path + ".classFilter"));
            var tags = new List<string>();
            JsonElement tagArray = RequiredArray(element, "tags", path + ".tags");
            foreach (JsonElement tag in tagArray.EnumerateArray()) tags.Add(RequiredString(tag, path + ".tags"));
            var metadata = new List<KeyValuePair<string, string>>();
            JsonElement metadataObject = RequiredObject(element, "metadata", path + ".metadata", options);
            if (metadataObject.EnumerateObject().Count() > options.MaximumMetadataEntries) throw Error("limit_exceeded", path + ".metadata", "Metadata count exceeds the configured JSON limit.");
            foreach (JsonProperty property in metadataObject.EnumerateObject()) metadata.Add(new KeyValuePair<string, string>(property.Name, RequiredString(property.Value, path + ".metadata." + property.Name)));
            IVisualRoiGeometry geometry;
            try
            {
                geometry = ReadGeometry(RequiredObject(element, "geometry", path + ".geometry", options), path + ".geometry", sourceSize, coordinateSpace, options);
            }
            catch (VisualRoiJsonException) { throw; }
            catch (ArgumentException exception) { throw Error("invalid_geometry", path + ".geometry", "The ROI geometry is invalid.", exception); }
            if (geometry.Kind == VisualRoiGeometryKind.Mask && (coordinateSpace == RoiCoordinateSpace.Normalized || coordinateSpace == RoiCoordinateSpace.ModelInput))
            {
                throw Error("invalid_geometry", path + ".coordinateSpace", "A mask ROI must use SourcePixels, TileLocal, or World coordinates.");
            }
            return new VisualRoi(id, geometry, coordinateSpace, name, enabled, priority, inclusion, execution, hitTest, threshold, margin, tasks, classes, tags, metadata, confidenceOverride);
        }

        private static IVisualRoiGeometry ReadGeometry(JsonElement element, string path, VisualSize sourceSize, RoiCoordinateSpace coordinateSpace, VisualRoiJsonOptions options)
        {
            string type = RequiredString(element, "type", path + ".type");
            if (string.Equals(type, nameof(VisualRoiGeometryKind.Rectangle), StringComparison.OrdinalIgnoreCase))
            {
                EnsureProperties(element, path, options, "type", "x", "y", "width", "height");
                return new RectangleRoiGeometry(new RectangleF(RequiredFloat(element, "x", path + ".x"), RequiredFloat(element, "y", path + ".y"), RequiredFloat(element, "width", path + ".width"), RequiredFloat(element, "height", path + ".height")));
            }
            if (string.Equals(type, nameof(VisualRoiGeometryKind.RotatedRectangle), StringComparison.OrdinalIgnoreCase))
            {
                EnsureProperties(element, path, options, "type", "centerX", "centerY", "width", "height", "angleDegrees");
                return new RotatedRectangleRoiGeometry(new PointF(RequiredFloat(element, "centerX", path + ".centerX"), RequiredFloat(element, "centerY", path + ".centerY")), new SizeF(RequiredFloat(element, "width", path + ".width"), RequiredFloat(element, "height", path + ".height")), RequiredFloat(element, "angleDegrees", path + ".angleDegrees"));
            }
            if (string.Equals(type, nameof(VisualRoiGeometryKind.Polygon), StringComparison.OrdinalIgnoreCase))
            {
                EnsureProperties(element, path, options, "type", "points");
                JsonElement points = RequiredArray(element, "points", path + ".points");
                if (points.GetArrayLength() > options.MaximumPolygonPoints) throw Error("limit_exceeded", path + ".points", "The polygon vertex count exceeds the configured JSON limit.");
                var values = new List<PointF>(points.GetArrayLength());
                int index = 0;
                foreach (JsonElement point in points.EnumerateArray())
                {
                    string pointPath = path + ".points[" + index + "]";
                    EnsureObject(point, pointPath, options);
                    EnsureProperties(point, pointPath, options, "x", "y");
                    values.Add(new PointF(RequiredFloat(point, "x", pointPath + ".x"), RequiredFloat(point, "y", pointPath + ".y")));
                    index++;
                }
                return new PolygonRoiGeometry(values);
            }
            if (string.Equals(type, nameof(VisualRoiGeometryKind.Mask), StringComparison.OrdinalIgnoreCase))
            {
                EnsureProperties(element, path, options, "type", "width", "height", "valuesBase64");
                int width = RequiredPositiveInt(element, "width", path + ".width");
                int height = RequiredPositiveInt(element, "height", path + ".height");
                if (coordinateSpace == RoiCoordinateSpace.SourcePixels && (width != sourceSize.Width || height != sourceSize.Height)) throw Error("invalid_geometry", path, "A SourcePixels mask geometry must match the document source size.");
                byte[] values = Convert.FromBase64String(RequiredString(element, "valuesBase64", path + ".valuesBase64"));
                if ((long)width * height != values.LongLength) throw Error("invalid_geometry", path, "The mask values length must equal width multiplied by height.");
                return new MaskRoiGeometry(new VisualSize(width, height), values);
            }
            throw Error("unsupported_geometry", path + ".type", "The ROI geometry type is not supported.");
        }

        private static void EnsureObject(JsonElement element, string path, VisualRoiJsonOptions options)
        {
            if (element.ValueKind != JsonValueKind.Object) throw Error("invalid_type", path, "Expected a JSON object.");
        }

        private static void EnsureProperties(JsonElement element, string path, VisualRoiJsonOptions options, params string[] allowed)
        {
            if (!options.RejectUnknownProperties) return;
            var names = new HashSet<string>(allowed, StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject()) if (!names.Contains(property.Name)) throw Error("unknown_property", path + "." + property.Name, "Unknown JSON property.");
        }

        private static JsonElement RequiredElement(JsonElement element, string name, string path)
        {
            if (!element.TryGetProperty(name, out JsonElement value)) throw Error("required", path, "Required JSON property is missing.");
            return value;
        }

        private static JsonElement RequiredObject(JsonElement element, string name, string path, VisualRoiJsonOptions options)
        {
            JsonElement value = RequiredElement(element, name, path);
            EnsureObject(value, path, options);
            return value;
        }

        private static JsonElement RequiredArray(JsonElement element, string name, string path)
        {
            JsonElement value = RequiredElement(element, name, path);
            if (value.ValueKind != JsonValueKind.Array) throw Error("invalid_type", path, "Expected a JSON array.");
            return value;
        }

        private static string RequiredString(JsonElement element, string name, string path) => RequiredString(RequiredElement(element, name, path), path);
        private static string RequiredString(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString())) throw Error("invalid_value", path, "Expected a non-empty JSON string.");
            return element.GetString()!;
        }
        private static bool RequiredBool(JsonElement element, string name, string path) { JsonElement value = RequiredElement(element, name, path); if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False) throw Error("invalid_type", path, "Expected a JSON boolean."); return value.GetBoolean(); }
        private static int RequiredInt(JsonElement element, string name, string path) => RequiredInt(RequiredElement(element, name, path), path);
        private static int RequiredInt(JsonElement element, string path) { if (!element.TryGetInt32(out int value)) throw Error("invalid_type", path, "Expected a JSON 32-bit integer."); return value; }
        private static long RequiredPositiveLong(JsonElement element, string name, string path) { JsonElement value = RequiredElement(element, name, path); if (!value.TryGetInt64(out long number) || number <= 0) throw Error("invalid_value", path, "Expected a positive JSON integer."); return number; }
        private static int RequiredPositiveInt(JsonElement element, string name, string path) { int value = RequiredInt(element, name, path); if (value <= 0) throw Error("invalid_value", path, "Expected a positive JSON integer."); return value; }
        private static float RequiredFloat(JsonElement element, string name, string path) { JsonElement value = RequiredElement(element, name, path); return RequiredFloat(value, path); }
        private static float RequiredFloat(JsonElement element, string path) { if (!element.TryGetSingle(out float value) || float.IsNaN(value) || float.IsInfinity(value)) throw Error("invalid_value", path, "Expected a finite JSON number."); return value; }

        private static T ParseEnum<T>(JsonElement element, string name, string path) where T : struct
        {
            string value = RequiredString(element, name, path);
            if (!Enum.TryParse<T>(value, true, out T parsed) || !Enum.IsDefined(typeof(T), parsed)) throw Error("invalid_value", path, "The enum value is not supported.");
            return parsed;
        }

        private static VisualRoiJsonException Error(string code, string path, string message, Exception? inner = null) => new VisualRoiJsonException(message, code, path, inner);
    }
}
