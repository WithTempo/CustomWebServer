using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;



class SimpleHttpServer
{
    static void Main()
    {

        int port = 8080;
        string rootDirectory = "wwwroot"; //Serve files from this directory

        if (!Directory.Exists(rootDirectory))
            Directory.CreateDirectory(rootDirectory);

        Socket serverSocket = new(
            AddressFamily.InterNetwork,
            SocketType.Stream,
            ProtocolType.Tcp
        );

        serverSocket.Bind(new IPEndPoint(IPAddress.Loopback, port));
        serverSocket.Listen(10);

        Console.WriteLine($"Server started on http://localhost:{port}/");

        while (true)
        {
            try
            {
                Socket clientSocket = serverSocket.Accept();
                HandleClient(clientSocket, rootDirectory);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Socket error: {ex.Message}");
            }
        }
    }
    static Dictionary<string, Dictionary<string, string>> sessionStore = new();
    static Dictionary<int, string> users = new(){
            {1, "Alice"},
            {2, "Bob"},
        };
    private static int nextUserId = 1;
    static void HandleClient(Socket clientSocket, string rootDirectory)
    {
        string responseMessage = ""; //define response message
        string body = ""; //declaring this so we can use it later
        byte[] buffer = new byte[4096]; //new array buffer takes 4096 bytes
        int bytesRead = clientSocket.Receive(buffer); // receives raw data from client
        string request = Encoding.UTF8.GetString(buffer, 0, bytesRead); //encodes raw data into json/plain-text/form/whatever else
        string[] requestLines = request.Split(new[] { "\r\n" }, StringSplitOptions.None); // splits the data by line
        string method = requestLines[0].Split(' ')[0]; //splits the first value in request lines array to get method i.e. GET, POST, PUT, DELETE
        string url = requestLines[0].Split(' ')[1];// second value give the url string for programming conditional logic
        string sessionId = "";
        string contentType = requestLines.FirstOrDefault(line => line.StartsWith("Content-Type:"))?.Split(' ')[1] ?? ""; //get's the line that starts with 'content-type' and whatver string comes immediately after so we know our content type and how to handle data
        string cookieHeader = requestLines.FirstOrDefault(line => line.StartsWith("Cookie:")) ?? "";
        int statusCode = 0;
        if (cookieHeader.Contains("sessionId="))
        {
            sessionId = cookieHeader.Split("sessionId=")[1].Split(';')[0];
        }
        else
        {
            sessionId = Guid.NewGuid().ToString();
            sessionStore[sessionId] = new Dictionary<string, string>();
        }
        
        if (!string.IsNullOrEmpty(cookieHeader))
        {
            Console.WriteLine($"Received Cookies: {cookieHeader}");

        }
        string extraHeaders = "";
        if (!cookieHeader.Contains("sessionId="))
        {
            extraHeaders = $"Set-Cookie: sessionId={sessionId}; Path=/; HttpOnly\r\n";
        }

        body = requestLines.Last(); Console.WriteLine($"URL = {url}");
        if (url == "/") //checks if url is a slash and renames it to index.html if it is
            url = "/index.html"; Console.WriteLine($"URL = {url}");
        string filePath = Path.Combine(rootDirectory, url.TrimStart('/')).Replace('\\', '/'); // removes slash from filePath, replaces double backslashes with single forward slash in filepath
        Console.WriteLine(filePath);  
        
        if (method == "POST")
        {
            Dictionary<string, string> userData = new();
            if (url == "/store-data")
            {   
                if (!sessionStore.ContainsKey(sessionId))
                {
                    sessionStore[sessionId] = new Dictionary<string, string>();
                }
                sessionStore[sessionId]["message"] = body;

                responseMessage = "Data stored in session!";
            }



            else if (url == "/submit")
            {

                Dictionary<string, string> formData = ParseFormData(body);
                string username = formData.ContainsKey("username") ? formData["username"] : "Unknown";
                string email = formData.ContainsKey("email") ? formData["email"] : "Unknown";

                responseMessage = $"<html><body><h1>Form Submitted!</h1><p>Username: {username}</p><p>Email: {email}</p></body></html>";
            }
            else if (url.StartsWith("/users"))
            {
                if (contentType.Contains("application/json"))
                {
                    Console.WriteLine($"{userData}");
                    try
                    {
                        userData = JsonSerializer.Deserialize<Dictionary<string, string>>(body) ?? new Dictionary<string, string>();
                    }
                    catch
                    {
                        responseMessage = "Invalid JSON format";
                        SendResponse(clientSocket, responseMessage, "text/plain", 400);
                        return;
                    }
                }
                else if (contentType.Contains("application/x-www-form-urlencoded"))
                {
                    userData = body.Split('&')
                        .Select(p => p.Split('='))
                        .Where(p => p.Length == 2)
                        .ToDictionary(
                            p => Uri.UnescapeDataString(p[0]),
                            p => Uri.UnescapeDataString(p[1])
                        );
                }
                else if (contentType.Contains("text/plain"))
                {
                    userData["name"] = body;
                }
                else
                {
                    responseMessage = "Unsupported Content-Type";
                    SendResponse(clientSocket, responseMessage, "text/plain", 415);
                    return;
                }

                if (userData != null && userData.ContainsKey("name"))
                {
                    users[nextUserId] = userData["name"];
                    responseMessage = $"User {nextUserId} created with name {userData["name"]}";
                    nextUserId++;
                }
                else
                {
                    responseMessage = "Invalid user data";
                    SendResponse(clientSocket, responseMessage, "text/plain", 400);
                    return;
                }
            }

            SendResponse(clientSocket, responseMessage, "text/html");
        }
        else if (method == "GET")
        {   
            if (File.Exists(filePath))
            {
            byte[] fileBytes = File.ReadAllBytes(filePath); // new array fileBytes, takes all data from file path
            contentType = GetContentType(filePath); // get's content type of filepath and asigns it to this string
            responseMessage = Encoding.UTF8.GetString(fileBytes);
            extraHeaders = "Set-Cookie: userId=12345; Path=/; HttpOnly\r\n";
            }
            else if(url.StartsWith("/users/"))
            {
                string[] parts = url.Split('/');
                if (parts.Length >= 3 && int.TryParse(parts[2], out int userId))
                {
                    responseMessage = users.ContainsKey(userId)
                        ? $"User {userId} is {users[userId]}"
                        : $"User with userId:{userId} does not exsits";
                }
                else
                {
                    responseMessage = "Invalid user ID format.";
                }
                contentType = "text/html";
                statusCode = 200;
            }
            else if (url.StartsWith("/get-data"))
            {
                foreach(var session in sessionStore)
                {
                    Console.WriteLine($"Session ID: {session.Key}, Data: {string.Join(", ", session.Value)}");
                }
                string storedMessage = sessionStore.ContainsKey(sessionId) && sessionStore[sessionId].ContainsKey("message")
                    ? sessionStore[sessionId]["message"]
                    : "No session data found.";

                contentType = "text/plain";
                statusCode = 200;
            }
            else SendNotFound(clientSocket);

            SendResponse(clientSocket, responseMessage, contentType, statusCode, extraHeaders);
        }
        else if (method == "PUT" && url.StartsWith("/users/"))
        {

            string[] parts = url.Split('/');
            Console.WriteLine($"URL: {url}");
            Console.WriteLine($"Parts Length: {parts.Length}");
            Console.WriteLine($"Parts: {string.Join(", ", parts)}");
            Console.WriteLine($"Extracted body: '{body}'");
            if (parts.Length >= 3 && int.TryParse(parts[2], out int userId))
            {
                if (users.ContainsKey(userId))
                {
                    users[userId] = body;
                    Console.WriteLine(body);
                    responseMessage = $"User {userId} updated to {body}";
                }
                else
                {
                    users[userId] = body;
                    Console.WriteLine(body);
                    responseMessage = $"User {userId} created with name {body}";
                }
            }
            else
            {
                responseMessage = "Invalid user ID format.";
            }

            SendResponse(clientSocket, responseMessage, "text/html");
        }
        else if (method == "PATCH" && url.StartsWith("/users/"))
        {
            string[] parts = url.Split('/');
            if (parts.Length >= 3 && int.TryParse(parts[2], out int userId))
            {
                if (users.ContainsKey(userId))
                {
                    users[userId] = body;
                    responseMessage = $"User {userId} partially updated to {body}";
                }
                else
                {
                    responseMessage = $"User {userId} was not found";
                }
            }
            else
            {
                responseMessage = "Invalid user ID format.";
            }

            SendResponse(clientSocket, responseMessage, "text/html");
        }
        else if (method == "DELETE" && url.StartsWith("/users/"))
        {
            string[] parts = url.Split('/');
            if (parts.Length >= 3 && int.TryParse(parts[2], out int userId))
            {
                responseMessage = users.Remove(userId)
                    ? $"User {userId} deleted"
                    : $"User {userId} not found.";
            }
            else
            {
                responseMessage = $"Invalid user ID Format.";
            }

            SendResponse(clientSocket, responseMessage, "text/html");
        }
        else
        {
            SendNotFound(clientSocket);
        }
        clientSocket.Shutdown(SocketShutdown.Both);
        clientSocket.Close();

    }


    static Dictionary<string, string> ParseFormData(string body)
    {
        var formData = new Dictionary<string, string>();
        string[] pairs = body.Split('&');
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

    static void SendResponse(Socket clientSocket, string content, string contentType, int statusCode = 200, string extraHeaders = "")
    {
        Console.WriteLine("Sending Response Message...");

        string responseHeader = $"HTTP/1.1 {statusCode} OK\r\n" +
                                $"Content-Type: {contentType}; charset=UTF-8\r\n" +
                                $"Content-Length: {content.Length}\r\n" +
                                $"{extraHeaders}" +
                                "Connection: close\r\n\r\n";

        clientSocket.Send(Encoding.UTF8.GetBytes(responseHeader));
        clientSocket.Send(Encoding.UTF8.GetBytes(content));
    }
    static void SendNotFound(Socket clientSocket)
    {
        string notFoundMessage = "<html><body><h1>404 - File Not Found</h1></body></html>";
        string responseHeader = $"HTTP/1.1 404 Not Found\r\nContent - Type: text/html\r\nContent-Length: {notFoundMessage.Length}\r\nConnection: close\r\n\r\n";
        clientSocket.Send(Encoding.UTF8.GetBytes(responseHeader));
        clientSocket.Send(Encoding.UTF8.GetBytes(notFoundMessage));

    }
    static string GetContentType(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();
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




}