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

        Console.WriteLine($"Server started on http://localhost:{port}/\n");

        while (true)
        {
            try
            {
                Socket clientSocket = serverSocket.Accept();
                HandleClient(clientSocket, rootDirectory);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Socket error: {ex.Message}\n");
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
        /* Setting Variables to avoid Null reference*/
        string method = "";
        string body = "";
        string url = "";
        string sessionId = "";
        string responseMessage = "";
        string extraHeaders = "";
        int statusCode = 0;
        byte[] buffer = new byte[4096]; //new array buffer takes 4096 bytes

        int bytesRead = clientSocket.Receive(buffer); // receives raw data from client
        /* bytes -> UTF8 */
        string request = Encoding.UTF8.GetString(buffer, 0, bytesRead); //encodes raw data into readable data
        /* Extracting information from header */
        string[] requestLines = request.Split(new[] { "\r\n" }, StringSplitOptions.None); // splits the data by line
        string method = requestLines[0].Split(' ')[0]; //splits the first value in request lines array to get method i.e. GET, POST, PUT, DELETE, PATCH
        string url = requestLines[0].Split(' ')[1];// second value give the url string for programming conditional logic
        string contentType = requestLines.FirstOrDefault(line => line.StartsWith("Content-Type:"))?.Split(' ')[1] ?? ""; //get's the line that starts with 'content-type' and whatver string comes immediately after so we know our content type and how to handle data
        string cookieHeader = requestLines.FirstOrDefault(line => line.StartsWith("Cookie:")) ?? ""; // gets' the line that starts with cookie and sets cookieheader to it
        
        if (!string.IsNullOrEmpty(cookieHeader) && cookieHeader.Contains("sessionId="))
        {
            Console.WriteLine($"Received Cookies: {cookieHeader}");
            sessionId = cookieHeader.Split("sessionId=")[1].Split(';')[0].Trim();
            Console.WriteLine($"Session Id: {sessionId}");
        }
        else 
        {   
            Console.WriteLine($"Cookie Header is Empty\n Creating new session Id...");
            sessionId = Guid.NewGuid().ToString();
            Console.WriteLine($"Created new session Id:{sessionId}");
        }
        

        body = requestLines.Last(); 
        if (url == "/") //checks if url is a slash and renames it to index.html if it is
            url = "/index.html"; 
            Console.WriteLine($"URL = {url}");
        string filePath = Path.Combine(rootDirectory, url.TrimStart('/')).Replace('\\', '/'); // removes slash from filePath, replaces double backslashes with single forward slash in filepath
        Console.WriteLine(filePath);

        switch (method)
        {
            case "GET": HandleGetReqeust(clientSocket, url, filePath, contentType, cookieHeader, sessionId); break;
            case "POST": HandlePostRequest(clientSocket, url, body, contentType, sessionId); break;
            case "PUT": HandlePutRequest(clientSocket, url, body); break;
            case "PATCH": HandlePatchRequest(clientSocket, url, body); break;
            case "DELETE": HandleDeleteRequest(clientSocket, url); break;
        }
        
        SendResponse(clientSocket, responseMessage, contentType, statusCode, responseMessage);
    }
        void HandlePostRequest(Socket clientSocket, string url, string body, string contentType, Guid sessionId){

            Dictionary<string, string> userData = new();
            userData["name"] = body;

            /* STORE-DATA */
            if(url=="/store-data") 
            {   
                if(!sessionStore.ContainsKey(sessionId)){
                    sessionStore[sessionId] = new Dictionary<string, string>();
                    Console.WriteLine("Stored session Id in Dictionary");
                }
                sessionStore[sessionId]["message"] = body;
                responseMessage = "Data stored in session!";
                contentType = "text/plain";
                statusCode = 200;
                extraHeaders = $"Set-Cookie: sessionId={sessionId}; Path=/; HttpOnly\r\n";
                Console.WriteLine($"Setting Cookie: sessionId={sessionId}");
            }
            /* SUBMIT */
            else if (url == "/submit")
            {
                Dictionary<string, string> formData = ParseFormData(body);
                string username = formData.ContainsKey("username") ? formData["username"] : "Unknown";
                string email = formData.ContainsKey("email") ? formData["email"] : "Unknown";
                responseMessage = $"<html><body><h1>Form Submitted!</h1><p>Username: {username}</p><p>Email: {email}</p></body></html>";
                contentType = "text/html";
                statusCode = 200;
            }
            /* USERS */
            else if (url == "/users")
            {
                
                statusCode = 200;

                if (contentType.Contains("application/json"))
                {
                    try
                    {
                        userData = JsonSerializer.Deserialize<Dictionary<string, string>>(body) ?? new Dictionary<string, string>();
                    }
                    catch
                    {
                        responseMessage = "Invalid JSON format";
                        statusCode = 400;
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
                    responseMessage = "User data successfully submitted";

                }
                else
                {
                    responseMessage = "Unsupported Content-Type";
                    statusCode = 415;
                    return;
                }

                if (userData.ContainsKey("name"))
                {
                    users[nextUserId] = userData["name"];
                    responseMessage = $"User {nextUserId} created with name {userData["name"]}";
                    nextUserId++;
                }
                else
                {
                    responseMessage = "Invalid user data";
                    statusCode = 400;
                    return;
                }
            }
            else{
                responseMessage = "Invalid URL";
                contentType = "text/plain";
                statusCode = 415;
            }
        }
        void HandleGetReqeust(Socket clientSocket, string url, string filePath, string contentType, string cookieHeader, Guid sessionId){  
        }

        if (method == "POST")
        {
            
            

            
            
            else if (url.StartsWith("/users"))
            {
                

            }

            SendResponse(clientSocket, responseMessage, contentType, statusCode, extraHeaders);
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
            else if (url.StartsWith("/users/"))
            {   
                Console.WriteLine("method==get, url starts with users");
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
            else if (url == "/get-data")
            {   
                Console.WriteLine($"received Cookie Header: {cookieHeader}");
                Console.WriteLine($"Extracted Session ID: {sessionId}");

                if(string.IsNullOrEmpty(sessionId) || !sessionStore.ContainsKey(sessionId))
                {
                    responseMessage = "No valid session found.";
                }
                else
                {
                    responseMessage = sessionStore[sessionId].ContainsKey("message")
                        ? sessionStore[sessionId]["message"]
                        : "No session data found.";
                }

                
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
        string statusText = statusCode switch
        {
            200 => "OK",
            400 => "Bad Request",
            404 => "Not Found",
            500 => "Internal Server Error",
            _=> "Unknown"
        };

        string responseHeader = $"HTTP/1.1 {statusCode} {statusText} OK\r\n" +
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