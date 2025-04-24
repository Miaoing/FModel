using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace TextureProcessing
{
    public class TextureProcessorCaller
    {
        private readonly string _executablePath;

        public TextureProcessorCaller(string executablePath = "texture_processor.exe")
        {
            _executablePath = executablePath;
        }

        public async Task<(bool Success, string Output, string Error)> ProcessTexturesAsync(string rootDirectory, string modelPath = null)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _executablePath,
                    Arguments = $"\"{rootDirectory}\"" + (modelPath != null ? $" --model \"{modelPath}\"" : ""),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                var output = "";
                var error = "";

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        output += e.Data + Environment.NewLine;
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        error += e.Data + Environment.NewLine;
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                return (process.ExitCode == 0, output, error);
            }
            catch (Exception ex)
            {
                return (false, "", $"Error launching texture processor: {ex.Message}");
            }
        }

        // Example usage
        public static async Task Main(string[] args)
        {
            var processor = new TextureProcessorCaller("path/to/texture_processor.exe");
            var result = await processor.ProcessTexturesAsync(
                rootDirectory: @"C:\path\to\json\files",
                modelPath: @"C:\path\to\model.joblib" // optional
            );

            if (result.Success)
            {
                Console.WriteLine("Processing completed successfully!");
                Console.WriteLine(result.Output);
            }
            else
            {
                Console.WriteLine("Processing failed:");
                Console.WriteLine(result.Error);
            }
        }
    }
} 