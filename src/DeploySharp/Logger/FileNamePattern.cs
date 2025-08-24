using log4net.Layout.Pattern;
using log4net.Core;
using System.IO;
using log4net.Layout;

namespace DeploySharp.Log
{

    /// <summary>
    /// Custom pattern converter that extracts just the filename from a full path for log messages
    /// </summary>
    public class FileNamePatternConverter : PatternLayoutConverter
    {
        /// <summary>
        /// Converts the full file path to just the filename for logging output
        /// </summary>
        /// <param name="writer">The text writer to write the converted value to</param>
        /// <param name="loggingEvent">The logging event containing location information</param>
        protected override void Convert(TextWriter writer, LoggingEvent loggingEvent)
        {
            var fullPath = loggingEvent.LocationInformation.FileName;
            if (!string.IsNullOrEmpty(fullPath))
            {
                var fileName = Path.GetFileName(fullPath); // Extract just filename
                writer.Write(fileName);
            }
            else
            {
                writer.Write("UnknownFile");
            }
        }
    }


    /// <summary>
    /// Custom PatternLayout that registers the filename converter
    /// </summary>
    public class CustomPatternLayout : PatternLayout
    {
        /// <summary>
        /// Initializes a new instance of CustomPatternLayout and registers the filename converter
        /// </summary>
        public CustomPatternLayout()
        {
            this.AddConverter("filename", typeof(FileNamePatternConverter));
        }
    }


}
