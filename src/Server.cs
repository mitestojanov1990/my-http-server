using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;

string myString = "some text";
char[] myStringChars = myString.ToCharArray();
Array.Reverse(myStringChars);
string reverseMyString = myStringChars.ToString();

bool isPalindrom = false;
string givenWord = "dadad";
var firstHalf = givenWord.Substring(0, givenWord.Length / 2);

char[] reversedArray = givenWord.ToCharArray();
Array.Reverse(reversedArray);
string reversedWord = new string(reversedArray);
var lastHalf = reversedWord.Substring(0, givenWord.Length / 2);

isPalindrom = firstHalf == lastHalf ? true : false;

isPalindrom = givenWord.SequenceEqual(givenWord.Reverse());
int theNum = 5;


Enumerable.Range(1, theNum).Where(x => x % 2 == 0).SequenceEqual(new[] { 1, theNum });

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

var hostIp = IPAddress.Any;
var hostPort = 4221;
// Uncomment this block to pass the first stage
TcpListener server = new TcpListener(hostIp, hostPort);
server.Start();

while (true)
{
    Console.WriteLine("waiting for requests");
    var socket = await server.AcceptSocketAsync(); // wait for client
    _ = Task.Run(() => ProcessRequest(socket));
}
byte[] CompressString(string input)
{
    using (var memoryStream = new MemoryStream())
    {
        using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
        {
            using (var writer = new StreamWriter(gzipStream))
            {
                writer.Write(input);
            }
        }
        return memoryStream.ToArray();
    }
}
async Task ProcessRequest(Socket socket)
{
    Console.WriteLine("New request received");
    var responseBuffer = new byte[1024];
    int receivedBytes = await socket.ReceiveAsync(responseBuffer, SocketFlags.None);

    var request = Encoding.UTF8.GetString(responseBuffer, 0, receivedBytes);
    var lines = request.Split("\r\n");

    var line0Parts = lines[0].Split(" ");
    var (method, path, httpVer) = (line0Parts[0], line0Parts[1], line0Parts[2]);

    bool supportsGzip = false;
    foreach (var line in lines.ToList())
    {
        if (line.StartsWith("Accept-Encoding:") && line.Contains("gzip"))
        {
            supportsGzip = true;
            break;
        }
    }
    string responseHeaders = string.Empty;
    byte[] responseBody = null;

    if (path.StartsWith("/user-agent"))
    {
        var line2 = lines[2].Split(" ");
        var (key, value) = (line2[0], line2[1]);
        Console.WriteLine($"This is the user agent: {value}");
        var contentLength = Encoding.UTF8.GetByteCount(value);
        responseBody = Encoding.UTF8.GetBytes(value);
        responseHeaders = $"{httpVer} 200 OK\r\n" + $"Content-Type: text/plain\r\n" +
                    $"Content-Length: {contentLength}\r\n" +
                    (supportsGzip ? $"Content-Encoding: gzip\r\n" : "\r\n");
    }
    else if (path.StartsWith("/files/"))
    {
        var fileStoragePath = "/not-set";
        if (args.Length >= 2 && args[0] == "--directory")
        {
            fileStoragePath = args[1];
        }

        var fileName = path.Split("/files/")[1];
        string pathToFile = $"{fileStoragePath}/{fileName}";

        var completeFilePath = Path.Combine(Directory.GetCurrentDirectory(), pathToFile);

        if (method == "GET")
        {
            if (File.Exists(completeFilePath))
            {
                string text = File.ReadAllText(completeFilePath);
                var contentLength = Encoding.UTF8.GetByteCount(text);
                responseBody = Encoding.UTF8.GetBytes(text);
                responseHeaders = $"{httpVer} 200 OK\r\n" + $"Content-Type: application/octet-stream\r\n" +
                            $"Content-Length: {contentLength}\r\n" +
                    (supportsGzip ? $"Content-Encoding: gzip\r\n" : "\r\n");
            }
            else
            {
                responseHeaders = $"{httpVer} 404 Not Found\r\n\r\n";
            }
        }
        else if (method == "POST")
        {
            if (File.Exists(completeFilePath))
            {
                Console.WriteLine($"409 Conflict");
                responseHeaders = $"{httpVer} 409 Conflict\r\n\r\n";
            }
            else
            {
                var postedData = request.Split("\r\n").Last();
                using (var fs = File.Create(completeFilePath))
                {
                    Byte[] content = new UTF8Encoding(true).GetBytes(postedData);
                    fs.Write(content, 0, content.Length);
                    responseHeaders = $"{httpVer} 201 Created\r\n\r\n";
                }
            }
        }
        else
        {
            responseHeaders = $"{httpVer} 405 Method not allowed\r\n\r\n";
        }

    }
    else if (path.StartsWith("/echo/"))
    {
        var echoStr = path.Substring("/echo/".Length);
        responseBody = supportsGzip ? CompressString(echoStr) : Encoding.UTF8.GetBytes(echoStr);
        var contentLength = responseBody.Length;
        responseHeaders = $"{httpVer} 200 OK\r\n" + $"Content-Type: text/plain\r\n" +
                    $"Content-Length: {contentLength}\r\n" +
                    (supportsGzip ? $"Content-Encoding: gzip\r\n\r\n" : "\r\n");
    }
    else if (path == "/" &&
                method.Equals("GET", StringComparison.OrdinalIgnoreCase))
    {
        responseHeaders = $"{httpVer} 200 OK\r\n" + (supportsGzip ? $"Content-Encoding: gzip\r\n" : "") + "\r\n";
    }
    else
    {
        Console.WriteLine($"This is the 404 not found");
        responseHeaders = $"{httpVer} 404 Not Found\r\n\r\n";
    }

    byte[] responseHeaderBytes = Encoding.UTF8.GetBytes(responseHeaders);
    await socket.SendAsync(responseHeaderBytes, SocketFlags.None);
    if (responseBody != null && responseBody.Length > 0)
    {
        await socket.SendAsync(responseBody, SocketFlags.None);
    }
    socket.Close();
}