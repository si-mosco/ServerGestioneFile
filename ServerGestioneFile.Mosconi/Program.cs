using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

class Server
{
    private const int Port = 6969; // Porta di base per il server
    private const int BasePort = 9000;
    private const string UsersFilePath = "users.txt";
    private static readonly object lockObject = new object();
    private const string ServerFolderPath = @"FileStorage\";

    static void Main(string[] args)
    {
        TcpListener listener = new TcpListener(IPAddress.Any, Port);
        listener.Start();
        Console.WriteLine("Server avviato. In attesa di connessioni...");

        int clientCounter = 0; // Contatore per tener traccia dei client

        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            Console.WriteLine("Client connesso.");

            int clientPort = BasePort + clientCounter; // Calcolo della porta per il nuovo client
            Task.Run(() => HandleClient(client, clientPort)); // Avvia un nuovo thread per gestire il client

            clientCounter++; // Incremento del contatore per il prossimo client
        }
    }

    static void HandleClient(TcpClient client, int clientPort)
    {
        using (NetworkStream stream = client.GetStream())
        using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
        using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            try
            {
                // Invia al client la porta su cui è in ascolto
                writer.Write(clientPort.ToString());

                // Crea un nuovo listener sulla porta specifica per questo client
                TcpListener clientListener = new TcpListener(IPAddress.Any, clientPort);
                clientListener.Start();

                Console.WriteLine($"Client in ascolto sulla porta {clientPort}");

                while (true)
                {
                    TcpClient clientSocket = clientListener.AcceptTcpClient();
                    Console.WriteLine($"Client collegato alla porta {clientPort}");

                    // Gestisce le richieste del client
                    HandleRequest(clientSocket);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore durante la gestione del client: {ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }
    }

    static void HandleRequest(TcpClient clientSocket)
    {
        using (NetworkStream stream = clientSocket.GetStream())
        using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
        using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            try
            {
                string command = reader.ReadString();
                switch (command)
                {
                    case "LOGIN":
                        Login(reader, writer);
                        break;
                    case "REGISTER":
                        Register(reader, writer);
                        break;
                    case "UPLOAD":
                        UploadFile(reader, writer);
                        break;
                    case "DOWNLOAD":
                        DownloadFile(reader, writer);
                        break;
                    case "LIST_FILES":
                        ListFile(reader, writer);
                        break;
                    default:
                        writer.Write("Comando non valido.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore durante l'elaborazione del comando: {ex.Message}");
                writer.Write($"Errore durante l'elaborazione del comando: {ex.Message}");
            }
        }
    }

    static void Login(BinaryReader reader, BinaryWriter writer)
    {
        string username = reader.ReadString();
        string password = reader.ReadString();

        if (AuthenticateUser(username, password))
        {
            writer.Write("OK");
        }
        else
        {
            writer.Write("ERROR");
        }
    }

    static void Register(BinaryReader reader, BinaryWriter writer)
    {
        string username = reader.ReadString();
        string password = reader.ReadString();

        if (!UserExists(username))
        {
            AddUser(username, password);
            Directory.CreateDirectory($"{ServerFolderPath + username}\\");
            writer.Write("OK");
        }
        else
        {
            writer.Write("ERROR");
        }
    }

    static void UploadFile(BinaryReader reader, BinaryWriter writer)
    {
        string fileName = reader.ReadString();
        int fileSize = reader.ReadInt32();
        byte[] fileData = reader.ReadBytes(fileSize);
        string name = reader.ReadString();

        if (name != "admin")
            File.WriteAllBytes($"{ServerFolderPath + name}/" + fileName, fileData);
        File.WriteAllBytes($"{ServerFolderPath}admin/" + fileName, fileData);

        writer.Write("File caricato con successo.");
    }

    static void DownloadFile(BinaryReader reader, BinaryWriter writer)
    {
        string requestedFile = reader.ReadString();
        string filePath = Path.Combine(ServerFolderPath, requestedFile);
        if (File.Exists(filePath))
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            writer.Write("OK");
            writer.Write(fileData.Length);
            writer.Write(fileData);
        }
        else
        {
            writer.Write("ERROR");
        }
    }

    static void ListFile(BinaryReader reader, BinaryWriter writer)
    {
        string name = reader.ReadString();
        // Invia l'elenco dei file disponibili al client
        Console.WriteLine(ServerFolderPath + name);
        string[] fileList = Directory.GetFiles(ServerFolderPath + name);
        writer.Write(fileList.Length);
        foreach (string file in fileList)
        {
            writer.Write(Path.GetFileName(file));
        }
    }

    static bool AuthenticateUser(string username, string password)
    {
        string hashedPassword = HashPassword(password);

        string[] users = File.ReadAllLines(UsersFilePath);
        foreach (string user in users)
        {
            string[] userInfo = user.Split(',');
            if (userInfo.Length == 2 && userInfo[0] == username && userInfo[1] == hashedPassword)
            {
                return true;
            }
        }

        return false;
    }

    static bool UserExists(string username)
    {
        string[] users = File.ReadAllLines(UsersFilePath);
        foreach (string user in users)
        {
            string[] userInfo = user.Split(',');
            if (userInfo.Length > 0 && userInfo[0] == username)
            {
                return true;
            }
        }

        return false;
    }

    static void AddUser(string username, string password)
    {
        string hashedPassword = HashPassword(password);
        string userEntry = $"{username},{hashedPassword}\n";

        lock (lockObject)
        {
            File.AppendAllText(UsersFilePath, userEntry);
        }
    }

    static string HashPassword(string password)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < hashedBytes.Length; i++)
            {
                builder.Append(hashedBytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }
}
