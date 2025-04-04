
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;

class RequestData 
{
    public string Body { get; set; } = "";
    public string SessionId { get; set; }= "";
    public string Method { get; set; } = "";
    public string Url {get;set;} = "";
    
    public string ContentType { get; set; } = "text/plain";
    public Dictionary<string, string> Headers { get; set; } = new();
    public Dictionary<string, string> UserData { get; set; } = new();
    public string FilePath {get; set;} = "";
}

class ServerState 
{
    public static Dictionary<string, Dictionary<string, string>> SessionStore { get; set; } = new();
    public static Dictionary<int, string> Users { get; set; } = new(){
            {1, "Alice"},
            {2, "Bob"},
        };
    private static int nextUserId = 1;

    public static int GetNextUserId() => nextUserId++;
}
class ResponseData 
{
    public string ResponseMessage { get; set; } = "";
    public string ExtraHeaders { get; set; } = "";
    public string ContentOut { get; set; } = "";
    public int StatusCode { get; set; } = 200;
}



class SimpleHttpServer
{
    static void Main()
    {

        int port = 8080;
        string rootDirectory = "wwwroot"; //Serve files from this directory
        RequestData request = new RequestData();
        ResponseData response = new ResponseData();
        ServerState state = new ServerState();
        if (!Directory.Exists(rootDirectory))
            Directory.CreateDirectory(rootDirectory);

        Socket serverSocket = new(
            AddressFamily.InterNetwork,
            SocketType.Stream,
            ProtocolType.Tcp
        );

        serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, port));
        serverSocket.Listen(10);

        Console.WriteLine($"Server started on http://localhost:{port}/\n");

        while (true)
        {
            try
            {
                Socket clientSocket = serverSocket.Accept();
                HandleClient(clientSocket, rootDirectory, request, response);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Socket error: {ex.Message}\n");
            }
        }
    }


    
    static void HandleClient(Socket clientSocket, string rootDirectory, RequestData request, ResponseData response)
    {   
     
        /* Set Variables In*/
       
        byte[] buffer = new byte[4096]; //new array buffer takes 4096 bytes
        int bytesRead = clientSocket.Receive(buffer); // receives raw data from client
        string requestEncoded = Encoding.UTF8.GetString(buffer, 0, bytesRead); //encodes raw data into readable data
        string[] requestLines = requestEncoded.Split(new[] { "\r\n" }, StringSplitOptions.None); // splits the data by line
        request.Method = requestLines[0].Split(' ')[0]; //splits the first value in request lines array to get method i.e. GET, POST, PUT, DELETE, PATCH
        request.Url = requestLines[0].Split(' ')[1];// second value give the url string for programming conditional logic
        request.Url = request.Url == "/" ? "/index.html" : request.Url;
        request.ContentType = requestLines.FirstOrDefault(line => line.StartsWith("Content-Type:"))?.Split(' ')[1] ?? ""; //get's the line that starts with 'content-type' and whatver string comes immediately after so we know our content type and how to handle data
        string cookieHeader = requestLines.FirstOrDefault(line => line.StartsWith("Cookie:")) ?? ""; // gets' the line that starts with cookie and sets cookieheader to it
        request.Body = requestLines.Last();
        /* Handle ResponseData */
        request.FilePath = Path.Combine(rootDirectory, request.Url.TrimStart('/')).Replace('\\','/');

        /* Check if cookie header exists, if so, extract sessionId of client, if not create new session Id for client */
        if (!string.IsNullOrEmpty(cookieHeader) && cookieHeader.Contains("sessionId="))
        {
            Console.WriteLine($"Received Cookies: {cookieHeader}");
            request.SessionId = cookieHeader.Split("sessionId=")[1].Split(';')[0].Trim().ToString();
            Console.WriteLine($"Session Id: {request.SessionId}");
        }
        else
        {
            Console.WriteLine($"Cookie Header is Empty\n Creating new session Id...");
            request.SessionId = Guid.NewGuid().ToString();
            Console.WriteLine($"Created new session Id:{request.SessionId}");
        }

        /* body is the actual content/data which we want the server to do something with */
        

        

        switch (request.Method)
        {
            case "GET": HandleGetRequest(clientSocket, request, response); break;
            case "POST": HandlePostRequest(clientSocket, request, response); break;
            case "PUT": HandlePutRequest(clientSocket, request, response); break;
            case "PATCH": HandlePatchRequest(clientSocket, request, response); break;
            case "DELETE": HandleDeleteRequest(clientSocket, request, response); break;

        }

        SendResponse(clientSocket, request, response);
        clientSocket.Shutdown(SocketShutdown.Both);
        clientSocket.Close();
    }
    static Dictionary<string, string> ParseFormData(string request)
    {   
        
        var formData = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(request))
        {
            Console.WriteLine("Form data is empty or null");
            return formData;
        }
        string[] pairs = request.Split('&');
        foreach (string pair in pairs)
        {
            string[] keyValue = pair.Split('=');
            if (keyValue.Length == 2)
            {
                string key = Uri.UnescapeDataString(keyValue[0]);
                string value = Uri.UnescapeDataString(keyValue[1]);
                formData[key] = value;
            }
        }
        return formData;
    }
    static void SendResponse(Socket clientSocket, RequestData request, ResponseData response)
    {
        Console.WriteLine("Sending Response Message...");
        string statusText = response.StatusCode switch
        {
            200 => "OK",
            400 => "Bad Request",
            404 => "Not Found",
            500 => "Internal Server Error",
            _ => "Unknown"
        };

        string responseHeader = $"HTTP/1.1 {response.StatusCode} {statusText} OK\r\n" +
                                $"Content-Type: {response.ContentOut}; charset=UTF-8\r\n" +
                                $"Content-Length: {response.ResponseMessage.Length}\r\n" +
                                $"{response.ExtraHeaders}" +
                                "Connection: close\r\n\r\n";
        

        clientSocket.Send(Encoding.UTF8.GetBytes(responseHeader));
        clientSocket.Send(Encoding.UTF8.GetBytes(response.ResponseMessage));
    }
    static void SendNotFound(Socket clientSocket, ResponseData response)
    {
        response.ResponseMessage = "<html><body><h1>404 - File Not Found</h1></body></html>";
        response.StatusCode = 404;
        response.ContentOut = "text/html";
    }
    static string GetContentType(string request)
    {
        string extension = Path.GetExtension(request).ToLower();
        return extension switch
        {
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".gif" => "image/gif",
            ".ico" => "image/x-icon",
            ".json" => "application/json",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }
    static void HandlePostRequest(Socket clientSocket, RequestData request, ResponseData response)
    {   
        if (request.ContentType != "application/json")
        {
            HandleError(response, 415, "Unsupported Content-Type");
            return;
        }
        if (request.Url == "/store-data")
        {
            if (!ServerState.SessionStore.ContainsKey(request.SessionId))
            {
                ServerState.SessionStore[request.SessionId] = new Dictionary<string, string>();
            }
            string sanitizedInput = SanitizeInput(request.Body);
            ServerState.SessionStore[request.SessionId]["message"] = sanitizedInput;
            response.ExtraHeaders = $"Set-Cookie: sessionId={request.SessionId}; Path=/; HttpOnly\r\n";
        }
        else if (request.Url == "/submit")
        {
            Dictionary<string, string> formData = ParseFormData(request.Body);
            string username = formData.ContainsKey("username") ? formData["username"] : "Unknown";
            string email = formData.ContainsKey("email") ? formData["email"] : "Unknown";
            response.ResponseMessage = $"<html><body><h1>Form Submitted!</h1><p>Username: {username}</p><p>Email: {email}</p></body></html>";
            response.ContentOut = "text/html";
        }
        else if (request.Url == "/users")
        {

            if (request.ContentType.Contains("application/json"))
            {
                try
                {
                    request.UserData = JsonSerializer.Deserialize<Dictionary<string, string>>(request.Body) ?? new Dictionary<string, string>();
                }
                catch
                {
                    response.ResponseMessage = "Invalid JSON format";
                    response.StatusCode = 400;
                    return;
                }
            }
            else if (request.ContentType.Contains("application/x-www-form-urlencoded"))
            {
                request.UserData = request.Body.Split('&')
                    .Select(p => p.Split('='))
                    .Where(p => p.Length == 2)
                    .ToDictionary(
                        p => Uri.UnescapeDataString(p[0]),
                        p => Uri.UnescapeDataString(p[1])
                    );
            }
            else if (request.ContentType.Contains("text/plain"))
            {
                request.UserData["name"] = request.Body;
            }
            else
            {
                response.ResponseMessage = "Unsupported Content-Type";
                response.StatusCode = 415;
                return;
            }

            if (request.UserData.ContainsKey("name"))
            {   
                int userId = ServerState.GetNextUserId();
                ServerState.Users[userId] = request.UserData["name"];
                response.ResponseMessage = $"User {userId} created with name {request.UserData["name"]}";
                
            }
            else
            {
                response.ResponseMessage = "Invalid user data";
                response.StatusCode = 400;
                return;
            }
        }
        else
        {
            response.ResponseMessage = "Invalid URL";
            response.ContentOut = "text/plain";
            response.StatusCode = 415;
        }
        
    }
    static void HandleGetRequest(Socket clientSocket, RequestData request, ResponseData response)
    {    
        if (File.Exists(request.FilePath))
        {
            byte[] fileBytes = File.ReadAllBytes(request.FilePath); // new array fileBytes, takes all data from file path
            request.ContentType = GetContentType(request.FilePath); // get's content type of filepath and asigns it to this string
            response.ResponseMessage = Encoding.UTF8.GetString(fileBytes);
            response.ExtraHeaders = "Set-Cookie: userId=12345; Path=/; HttpOnly\r\n";
        }
        else if (request.Url.StartsWith("/users/"))
        {
            string[] parts = request.Url.Split('/');
            if (parts.Length >= 3 && int.TryParse(parts[2], out int userId))
            {
                response.ResponseMessage = ServerState.Users.ContainsKey(userId)
                    ? $"User {userId} is {ServerState.Users[userId]}"
                    : $"User with userId:{userId} does not exsits";
            }
            else
            {
                response.ResponseMessage = "Invalid user ID format.";
            }
            response.ContentOut = "text/html";
        }
        else if (request.Url == "/get-data")
        {
            if (string.IsNullOrEmpty(request.SessionId) || !ServerState.SessionStore.ContainsKey(request.SessionId))
            {
                response.ResponseMessage = "No valid session found.";
            }
            else
            {   
                response.ResponseMessage = ServerState.SessionStore[request.SessionId].ContainsKey("message")
                    ? ServerState.SessionStore[request.SessionId]["message"]
                    : "No session data found.";
            }

        }
        else SendNotFound(clientSocket, response);

    }
    static void HandlePutRequest(Socket clientSocket, RequestData request, ResponseData response)
    {
        if (request.Url.StartsWith("/users/"))
        {
            string[] parts = request.Url.Split('/');
            if (parts.Length >= 3 && int.TryParse(parts[2], out int userId))
            {
                ServerState.Users[userId] = request.Body;
                if (ServerState.Users.ContainsKey(userId))
                {
                    Console.WriteLine(request.Body);
                }
                else
                {
                    Console.WriteLine(request.Body);
                }
            }
            else
            {
                response.ResponseMessage = "Invalid user ID format.";
            }
        }
    }
    static void HandlePatchRequest(Socket clientSocket, RequestData request, ResponseData response)
    {
        if (request.Url.StartsWith("/users"))
        {
            string[] parts = request.Url.Split('/');
            if (parts.Length >= 3 && int.TryParse(parts[2], out int userId))
            {
                if (ServerState.Users.ContainsKey(userId))
                {
                    ServerState.Users[userId] = request.Body;
                    response.ResponseMessage = $"User {userId} partially updated to {request.Body}";
                }
                else
                {
                    response.ResponseMessage = $"User {userId} was not found";
                }
            }
            else
            {
                response.ResponseMessage = "Invalid user ID format.";
            }
        }
        else SendNotFound(clientSocket, response);
    }
    static void HandleDeleteRequest(Socket clientSocket, RequestData request, ResponseData response)
    {
        if (request.Url.StartsWith("/users"))
        {
            string[] parts = request.Url.Split('/');
            if (parts.Length >= 3 && int.TryParse(parts[2], out int userId))
            {
                response.ResponseMessage = ServerState.Users.Remove(userId)
                    ? $"User {userId} deleted"
                    : $"User {userId} not found.";
            }
            else
            {
                response.ResponseMessage = $"Invalid user ID Format.";
            }
        }
        else SendNotFound(clientSocket, response);
    }



    static void HandleError(ResponseData response, int statusCode, string errorMessage)
    {
        response.StatusCode = statusCode;
        response.ResponseMessage = errorMessage;
        response.ContentOut = "text/html";
    }
    static string SanitizeInput(string input)
    {
        return WebUtility.HtmlEncode(input);
    }
    static void Log(string message)
    {
        string logFile = "server.log";
        File.AppendAllText(logFile, $"{DateTime.Now}: {message}\n");
    }
    static void HandleFileUpload(RequestData request, ResponseData response)
    {
        if(!request.ContentType.Contains("multipart/form-data"))
        {
            HandleError(response, 415, "Unsupported Content-Type");
            return;
        }
        string uploadedFilePath = Path.Combine("uploads", Guid.NewGuid().ToString());
        File.WriteAllText(uploadedFilePath, request.Body);
        response.ResponseMessage = $"File saved to {uploadedFilePath}";
    }
}