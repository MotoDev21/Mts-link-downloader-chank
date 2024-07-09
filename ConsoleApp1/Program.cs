using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using MediaToolkit;
using MediaToolkit.Model;
using MediaToolkit.Options;
using NAudio.Wave;

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private static string baseUrlVideo;
    private static string baseUrlAudio;
    private static readonly string videoFolder = "Video";
    private static readonly string audioFolder = "Audio";
    private static readonly string outputFolder = "Output";

    static async Task Main(string[] args)
    {
        Console.Write("Введите базовый URL для видео: ");
        baseUrlVideo = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(baseUrlVideo))
        {
            Console.WriteLine("URL не может быть пустым. Завершение программы.");
            return;
        }

        if (!baseUrlVideo.EndsWith("/"))
        {
            baseUrlVideo += "/";
        }

        Directory.CreateDirectory(videoFolder);
        Directory.CreateDirectory(audioFolder);
        Directory.CreateDirectory(outputFolder);

        await DownloadFiles("media_", ".ts", videoFolder, baseUrlVideo);

        Console.Write("Введите базовый URL для аудио: ");
        baseUrlAudio = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(baseUrlAudio))
        {
            Console.WriteLine("URL не может быть пустым. Завершение программы.");
            return;
        }

        if (!baseUrlAudio.EndsWith("/"))
        {
            baseUrlAudio += "/";
        }

        await DownloadFiles("media_", ".aac", audioFolder, baseUrlAudio);

        string combinedVideoPath = CombineVideoFiles();
        string combinedAudioPath = CombineAudioFiles();

        Console.WriteLine("Объединяю файлы в папке Output! Не закрывайте окно :)");
        CombineVideoAudio(combinedVideoPath, combinedAudioPath);

        Console.WriteLine("Готово! Объединённый файл находится в папке Output.");
        Console.ReadKey(true);
    }

    private static async Task DownloadFiles(string prefix, string extension, string folder, string baseUrl)
    {
        int index = 0;
        while (true)
        {
            string fileUrl = $"{baseUrl}{prefix}{index}{extension}";
            string localFilePath = Path.Combine(folder, $"{index}{extension}");

            try
            {
                Console.WriteLine($"Downloading {fileUrl}...");
                HttpResponseMessage response = await client.GetAsync(fileUrl);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine("No more files found.");
                    break;
                }

                response.EnsureSuccessStatusCode();

                using (var fs = new FileStream(localFilePath, FileMode.CreateNew))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Error downloading file: {e.Message}");
                break;
            }
            index++;
        }
    }

    private static string CombineVideoFiles()
    {
        string outputPath = Path.Combine(outputFolder, "combined.ts");

        using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        {
            for (int i = 0; ; i++)
            {
                string filePath = Path.Combine(videoFolder, $"{i}.ts");
                if (!File.Exists(filePath))
                {
                    break;
                }

                using (var input = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    input.CopyTo(output);
                }
            }
        }

        return outputPath;
    }

    private static string CombineAudioFiles()
    {
        string outputPath = Path.Combine(outputFolder, "combined.aac");

        using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        {
            for (int i = 0; ; i++)
            {
                string filePath = Path.Combine(audioFolder, $"{i}.aac");
                if (!File.Exists(filePath))
                {
                    break;
                }

                using (var input = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    input.CopyTo(output);
                }
            }
        }

        return outputPath;
    }

    private static void CombineVideoAudio(string videoPath, string audioPath)
    {
        string outputPath = Path.Combine(outputFolder, "output.mp4");

        // Ensure audio is converted to WAV format for easier merging
        string tempAudioPath = Path.Combine(outputFolder, "temp.wav");
        ConvertAudioToWav(audioPath, tempAudioPath);

        // Combine video and audio
        var inputFile = new MediaFile { Filename = videoPath };
        var outputFile = new MediaFile { Filename = outputPath };

        using (var engine = new Engine())
        {
            var options = new ConversionOptions { AudioSampleRate = AudioSampleRate.Hz44100 };
            engine.CustomCommand($"-i \"{videoPath}\" -i \"{tempAudioPath}\" -c:v copy -c:a aac \"{outputPath}\"");
        }

        // Clean up temporary audio file
        File.Delete(tempAudioPath);
    }

    private static void ConvertAudioToWav(string inputPath, string outputPath)
    {
        using (var reader = new AudioFileReader(inputPath))
        using (var writer = new WaveFileWriter(outputPath, reader.WaveFormat))
        {
            reader.CopyTo(writer);
        }
    }
}
