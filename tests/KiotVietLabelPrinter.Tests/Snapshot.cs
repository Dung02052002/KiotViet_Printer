using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

/// <summary>
/// Golden-master: lần chạy đầu ghi kết quả hiện tại ra Snapshots/*.json (là
/// "hành vi chuẩn"), các lần sau so khớp từng ký tự. Lệch → test đỏ và bản
/// thực tế được ghi cạnh đó (*.received.json) để diff.
/// Muốn chấp nhận hành vi mới: xoá file .json tương ứng rồi chạy lại.
/// </summary>
internal static class Snapshot
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Match(
        object value,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "")
    {
        string dir = Path.Combine(Path.GetDirectoryName(file)!, "Snapshots");
        Directory.CreateDirectory(dir);

        string name = $"{Path.GetFileNameWithoutExtension(file)}.{member}";
        string path = Path.Combine(dir, name + ".json");
        string received = Path.Combine(dir, name + ".received.json");

        string actual = JsonSerializer.Serialize(value, Options).ReplaceLineEndings("\n");

        if (!File.Exists(path))
        {
            File.WriteAllText(path, actual);
            return;
        }

        string expected = File.ReadAllText(path).ReplaceLineEndings("\n");
        if (expected == actual)
        {
            File.Delete(received);
            return;
        }

        File.WriteAllText(received, actual);
        Assert.Equal(expected, actual);
    }
}
