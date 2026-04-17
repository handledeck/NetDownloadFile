using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

class TcpFileClient
{
  private const string ConfigFile = "client_config.txt";
  private const string HistoryFile = "command_history.txt";
  private const int PacketSize = 512;
  private const int ServerPort = 55555;
  private static string ServerIp = "";
  private static string lastDownloadedFile = "";
  private static string lastRequestedFile = "";
  private static List<string> ipHistory = new List<string>();
  private static List<string> commandHistory = new List<string>();
  private static int historyIndex = -1;
  private static string currentInput = "";

  static async Task Main(string[] args)
  {
    Console.OutputEncoding = Encoding.UTF8;
    Console.Title = "TCP файловый клиент";

    LoadIpHistory();
    LoadCommandHistory();

    bool connected = false;
    while (!connected)
    {
      ServerIp = await SelectOrEnterIp();
      connected = await TestConnection(ServerIp);
      if (!connected)
      {
        Console.WriteLine("\n  [!] Не удалось подключиться к серверу {0}:{1}", ServerIp, ServerPort);
        Console.WriteLine("  Нажмите любую клавишу для продолжения...");
        Console.ReadKey(true);
      }
    }

    SaveIpToHistory(ServerIp);

    try
    {
      Console.WindowWidth = 120;
      Console.BufferWidth = 120;
      Console.WindowHeight = 40;
    }
    catch { }

    await MainMenu();
  }

  static void LoadCommandHistory()
  {
    try
    {
      if (File.Exists(HistoryFile))
      {
        string[] lines = File.ReadAllLines(HistoryFile);
        foreach (string line in lines)
        {
          string cmd = line.Trim();
          if (!string.IsNullOrWhiteSpace(cmd) && !commandHistory.Contains(cmd))
          {
            commandHistory.Add(cmd);
          }
        }
      }
    }
    catch { }
  }

  static void SaveCommandToHistory(string command)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(command)) return;

      if (commandHistory.Contains(command))
      {
        commandHistory.Remove(command);
      }

      commandHistory.Insert(0, command);

      while (commandHistory.Count > 50)
      {
        commandHistory.RemoveAt(commandHistory.Count - 1);
      }

      File.WriteAllLines(HistoryFile, commandHistory);
    }
    catch { }
  }

  static void LoadIpHistory()
  {
    try
    {
      if (File.Exists(ConfigFile))
      {
        string[] lines = File.ReadAllLines(ConfigFile);
        foreach (string line in lines)
        {
          string ip = line.Trim();
          if (!string.IsNullOrWhiteSpace(ip) && !ipHistory.Contains(ip) && IsValidIp(ip))
          {
            ipHistory.Add(ip);
          }
        }
      }
    }
    catch { }
  }

  static void SaveIpToHistory(string ip)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(ip) || !IsValidIp(ip)) return;

      if (ipHistory.Contains(ip))
      {
        ipHistory.Remove(ip);
      }
      ipHistory.Insert(0, ip);

      while (ipHistory.Count > 40)
      {
        ipHistory.RemoveAt(ipHistory.Count - 1);
      }

      File.WriteAllLines(ConfigFile, ipHistory);
    }
    catch { }
  }

  static bool IsValidIp(string ip)
  {
    if (string.IsNullOrWhiteSpace(ip)) return false;

    Regex ipRegex = new Regex(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$");
    return ipRegex.IsMatch(ip);
  }

  static async Task<bool> TestConnection(string ip)
  {
    try
    {
      Console.WriteLine("\n  Проверка подключения к серверу {0}:{1}...", ip, ServerPort);
      using (TcpClient client = new TcpClient())
      {
        var connectTask = client.ConnectAsync(ip, ServerPort);
        if (await Task.WhenAny(connectTask, Task.Delay(3000)) == connectTask)
        {
          await connectTask;
          Console.WriteLine("  [+] Подключение успешно!");
          return true;
        }
        else
        {
          Console.WriteLine("  [-] Таймаут подключения (3 секунды)");
          return false;
        }
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine("  [-] Ошибка подключения: {0}", ex.Message);
      return false;
    }
  }

  static async Task<string> SelectOrEnterIp()
  {
    if (ipHistory.Count == 0)
    {
      return await EnterNewIp();
    }

    List<string> menuItems = new List<string>();
    foreach (string ip in ipHistory)
    {
      menuItems.Add(ip);
    }
    menuItems.Add("[Ввести новый IP-адрес]");

    int selectedIndex = 0;
    int scrollOffset = 0;
    int visibleItems = 20;
    bool exitMenu = false;
    string selectedIp = null;

    while (!exitMenu)
    {
      Console.Clear();

      int windowWidth = Console.WindowWidth - 1;
      string topLine = "+" + new string('-', windowWidth - 2) + "+";
      string title = "ВЫБОР IP-АДРЕСА СЕРВЕРА";
      int padding = (windowWidth - 2 - title.Length) / 2;
      if (padding < 0) padding = 0;
      string titleLine = "|" + new string(' ', padding) + title + new string(' ', windowWidth - 2 - padding - title.Length) + "|";

      Console.WriteLine(topLine);
      Console.WriteLine(titleLine);
      Console.WriteLine(topLine);
      Console.WriteLine();
      Console.WriteLine("  Используйте [↑] [↓] для навигации, [Enter] для выбора, [Esc] для выхода");
      Console.WriteLine($"  Всего сохранено адресов: {ipHistory.Count}/40");
      Console.WriteLine();

      if (selectedIndex < scrollOffset)
      {
        scrollOffset = selectedIndex;
      }
      else if (selectedIndex >= scrollOffset + visibleItems)
      {
        scrollOffset = selectedIndex - visibleItems + 1;
      }

      for (int i = 0; i < visibleItems && (scrollOffset + i) < menuItems.Count; i++)
      {
        int itemIndex = scrollOffset + i;
        string displayText = menuItems[itemIndex];

        if (itemIndex < ipHistory.Count && itemIndex == 0)
        {
          displayText += " (последний использованный)";
        }

        if (itemIndex == selectedIndex)
        {
          Console.ForegroundColor = ConsoleColor.Green;
          Console.BackgroundColor = ConsoleColor.DarkGray;
          Console.Write("  >> ");
          Console.Write(displayText);
          Console.ResetColor();
          Console.WriteLine();
        }
        else
        {
          Console.Write("     ");
          Console.WriteLine(displayText);
        }
      }

      if (menuItems.Count > visibleItems)
      {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;

        if (scrollOffset > 0)
        {
          Console.Write("     ↑ еще " + scrollOffset + " адресов выше ↑");
        }

        if (scrollOffset + visibleItems < menuItems.Count)
        {
          if (scrollOffset > 0) Console.Write("   ");
          Console.Write("↓ еще " + (menuItems.Count - (scrollOffset + visibleItems)) + " адресов ниже ↓");
        }

        Console.ResetColor();
      }

      Console.WriteLine();
      if (ipHistory.Count > 0)
      {
        Console.WriteLine("  Текущий сохраненный IP: {0}", ipHistory[0]);
      }

      ConsoleKeyInfo key = Console.ReadKey(true);

      switch (key.Key)
      {
        case ConsoleKey.UpArrow:
          if (selectedIndex > 0)
          {
            selectedIndex--;
          }
          else
          {
            selectedIndex = menuItems.Count - 1;
            scrollOffset = Math.Max(0, menuItems.Count - visibleItems);
          }
          break;
        case ConsoleKey.DownArrow:
          if (selectedIndex < menuItems.Count - 1)
          {
            selectedIndex++;
          }
          else
          {
            selectedIndex = 0;
            scrollOffset = 0;
          }
          break;
        case ConsoleKey.Escape:
          Console.Clear();
          Console.WriteLine("До свидания!");
          Environment.Exit(0);
          break;
        case ConsoleKey.Enter:
          if (selectedIndex < ipHistory.Count)
          {
            selectedIp = ipHistory[selectedIndex];
            exitMenu = true;
          }
          else
          {
            string newIp = await EnterNewIp();
            if (newIp != null)
            {
              selectedIp = newIp;
              exitMenu = true;
            }
          }
          break;
      }
    }

    return selectedIp;
  }

  static async Task<string> EnterNewIp()
  {
    while (true)
    {
      Console.Clear();

      int windowWidth = Console.WindowWidth - 1;
      string topLine = "+" + new string('-', windowWidth - 2) + "+";
      string title = "ВВОД НОВОГО IP-АДРЕСА";
      int padding = (windowWidth - 2 - title.Length) / 2;
      if (padding < 0) padding = 0;
      string titleLine = "|" + new string(' ', padding) + title + new string(' ', windowWidth - 2 - padding - title.Length) + "|";

      Console.WriteLine(topLine);
      Console.WriteLine(titleLine);
      Console.WriteLine(topLine);
      Console.WriteLine();
      Console.WriteLine("  (Esc для возврата к выбору IP)");
      Console.WriteLine();
      Console.Write("  Введите IP-адрес (например: 192.168.1.100): ");

      string ip = await ReadLineWithEscapeAsync();
      if (ip == null) return null;

      if (string.IsNullOrWhiteSpace(ip))
      {
        Console.WriteLine("\n  [!] IP-адрес не может быть пустым");
        Console.WriteLine("\n  Нажмите любую клавишу для повтора...");
        Console.ReadKey(true);
        continue;
      }

      if (!IsValidIp(ip))
      {
        Console.WriteLine("\n  [!] Неверный формат IP-адреса");
        Console.WriteLine("  Пример правильного формата: 192.168.1.100");
        Console.WriteLine("\n  Нажмите любую клавишу для повтора...");
        Console.ReadKey(true);
        continue;
      }

      if (await TestConnection(ip))
      {
        return ip;
      }
      else
      {
        Console.WriteLine("\n  [!] Не удалось подключиться к серверу {0}:{1}", ip, ServerPort);
        Console.WriteLine("\n  [1] Повторить ввод");
        Console.WriteLine("  [2] Вернуться к выбору IP");
        Console.WriteLine("  [Esc] Выход");

        var key = Console.ReadKey(true);
        if (key.Key == ConsoleKey.Escape)
        {
          Console.Clear();
          Console.WriteLine("До свидания!");
          Environment.Exit(0);
        }
        else if (key.Key == ConsoleKey.D1 || key.Key == ConsoleKey.NumPad1)
        {
          continue;
        }
        else
        {
          return null;
        }
      }
    }
  }

  static async Task MainMenu()
  {
    string[] menuItems = { "Показать список файлов в директории (DIR)",
                           "Скачать файл (FILE)",
                           "Скачать app.log + все .tgz (GETLOGS)",
                           "Выполнить команду (CMD)",
                           "Сетевой тест (NET_TEST)",
                           "Сменить сервер (F2)",
                           "Выход" };
    int selectedIndex = 0;

    while (true)
    {
      Console.Clear();

      int windowWidth = Console.WindowWidth - 1;
      string topLine = "+" + new string('-', windowWidth - 2) + "+";
      string title = "TCP ФАЙЛОВЫЙ КЛИЕНТ";
      int padding = (windowWidth - 2 - title.Length) / 2;
      if (padding < 0) padding = 0;
      string titleLine = "|" + new string(' ', padding) + title + new string(' ', windowWidth - 2 - padding - title.Length) + "|";

      Console.WriteLine(topLine);
      Console.WriteLine(titleLine);
      Console.WriteLine(topLine);
      Console.WriteLine();
      Console.WriteLine("  Используйте [↑] [↓] для навигации, [Enter] для выбора, [Esc] для выхода");
      Console.WriteLine();

      for (int i = 0; i < menuItems.Length; i++)
      {
        if (i == selectedIndex)
        {
          Console.ForegroundColor = ConsoleColor.Green;
          Console.BackgroundColor = ConsoleColor.DarkGray;
          Console.Write("  >> ");
          Console.Write(menuItems[i]);
          Console.ResetColor();
          Console.WriteLine();
        }
        else
        {
          Console.Write("     ");
          Console.WriteLine(menuItems[i]);
        }
      }

      Console.WriteLine();
      Console.WriteLine("  Текущий сервер: {0}:{1}", ServerIp, ServerPort);

      ConsoleKeyInfo key = Console.ReadKey(true);

      switch (key.Key)
      {
        case ConsoleKey.UpArrow:
          selectedIndex = (selectedIndex - 1 + menuItems.Length) % menuItems.Length;
          break;
        case ConsoleKey.DownArrow:
          selectedIndex = (selectedIndex + 1) % menuItems.Length;
          break;
        case ConsoleKey.Escape:
          Console.Clear();
          Console.WriteLine("До свидания!");
          return;
        case ConsoleKey.F2:
          await ChangeServer();
          break;
        case ConsoleKey.Enter:
          Console.Clear();
          if (selectedIndex == 0)
          {
            await HandleDirCommand();
          }
          else if (selectedIndex == 1)
          {
            await HandleFileDownload();
          }
          else if (selectedIndex == 2)
          {
            await HandleGetLogs();
          }
          else if (selectedIndex == 3)
          {
            await HandleCmdCommand();
          }
          else if (selectedIndex == 4)
          {
            await HandleNetTestCommand();
          }
          else if (selectedIndex == 5)
          {
            await ChangeServer();
          }
          else if (selectedIndex == 6)
          {
            Console.WriteLine("До свидания!");
            return;
          }
          break;
      }
    }
  }

  static async Task HandleGetLogs()
  {
    while (true)
    {
      Console.Clear();

      int windowWidth = Console.WindowWidth - 1;
      string topLine = "+" + new string('-', windowWidth - 2) + "+";
      string title = "СКАЧИВАНИЕ LOG-ФАЙЛОВ И АРХИВОВ";
      int padding = (windowWidth - 2 - title.Length) / 2;
      if (padding < 0) padding = 0;
      string titleLine = "|" + new string(' ', padding) + title + new string(' ', windowWidth - 2 - padding - title.Length) + "|";

      Console.WriteLine(topLine);
      Console.WriteLine(titleLine);
      Console.WriteLine(topLine);
      Console.WriteLine();
      Console.WriteLine("  Esc - выход в главное меню");
      Console.WriteLine();
      Console.Write("  Введите путь к директории (Enter - текущая): ");

      string dirpath = await ReadLineWithEscapeAsync();
      if (dirpath == null) return;

      try
      {
        List<string> files = await GetFileList(dirpath);

        List<LogFile> filesToDownload = new List<LogFile>();

        foreach (string file in files)
        {
          if (file.Equals("app.log", StringComparison.OrdinalIgnoreCase))
          {
            filesToDownload.Add(new LogFile { Name = file, IsTgz = false });
          }
          else if (file.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
          {
            filesToDownload.Add(new LogFile { Name = file, IsTgz = true });
          }
        }

        if (filesToDownload.Count == 0)
        {
          Console.WriteLine("\n  [!] Не найдено ни app.log, ни .tgz файлов");
          Console.WriteLine("\n  Нажмите любую клавишу для продолжения...");
          Console.ReadKey(true);
          continue;
        }

        Console.WriteLine("\n  Найдено:");
        foreach (var file in filesToDownload)
        {
          Console.WriteLine($"    - {file.Name}");
        }

        Console.WriteLine($"\n  Скачать {filesToDownload.Count} файл(ов)?");
        Console.Write("  [Y] Да / [N] Нет: ");

        var key = Console.ReadKey(true);
        if (key.Key != ConsoleKey.Y)
        {
          continue;
        }

        Console.WriteLine("  Да");

        // Создаем папку с IP адресом
        string ipFolder = ServerIp.Replace('.', '_');
        string downloadDir = Path.Combine(Directory.GetCurrentDirectory(), ipFolder);

        // Если папка существует - очищаем её
        if (Directory.Exists(downloadDir))
        {
          Console.WriteLine($"\n  [!] Папка {ipFolder} существует, очищаем...");
          foreach (string file in Directory.GetFiles(downloadDir))
          {
            File.Delete(file);
          }
        }
        else
        {
          Directory.CreateDirectory(downloadDir);
          Console.WriteLine($"\n  [+] Создана папка: {ipFolder}/");
        }

        int successCount = 0;
        for (int i = 0; i < filesToDownload.Count; i++)
        {
          var file = filesToDownload[i];
          string fullPath = string.IsNullOrEmpty(dirpath) ? file.Name : $"{dirpath}/{file.Name}";

          Console.Write($"\n  [{i + 1}/{filesToDownload.Count}] Скачивание {file.Name}... ");

          lastRequestedFile = fullPath;

          try
          {
            using (TcpClient client = new TcpClient())
            {
              await client.ConnectAsync(ServerIp, ServerPort);

              using (NetworkStream stream = client.GetStream())
              {
                string request = $"{fullPath}\n";
                byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                await stream.WriteAsync(requestBytes, 0, requestBytes.Length);

                await ReceiveDataSilent(stream, file.Name);
              }
            }

            if (!string.IsNullOrEmpty(lastDownloadedFile) && File.Exists(lastDownloadedFile))
            {
              string finalFileName;
              if (!file.IsTgz)
              {
                DateTime now = DateTime.Now;
                string timestamp = now.ToString("dd-MM-yy_HH-mm-ss");
                finalFileName = $"app_{timestamp}.log";
                file.DisplayDate = now;
              }
              else
              {
                finalFileName = file.Name;
                file.DisplayDate = ParseDateFromTgzName(file.Name);
              }

              file.FullPath = Path.Combine(downloadDir, finalFileName);
              if (File.Exists(file.FullPath)) File.Delete(file.FullPath);
              File.Move(lastDownloadedFile, file.FullPath);
              file.Downloaded = true;
              successCount++;
            }
          }
          catch (Exception ex)
          {
            Console.WriteLine($"\n  ОШИБКА: {ex.Message}");
            file.Downloaded = false;
          }
        }

        Console.WriteLine($"\n  [+] Скачано {successCount} из {filesToDownload.Count} файлов");

        // Объединяем файлы, если есть tgz или app.log
        var downloadedFiles = filesToDownload.Where(f => f.Downloaded).ToList();
        if (downloadedFiles.Count > 0)
        {
          await MergeLogFiles(downloadedFiles, downloadDir);
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"\n  Ошибка: {ex.Message}");
      }

      Console.WriteLine("\n  Нажмите любую клавишу для продолжения...");
      Console.ReadKey(true);
    }
  }

  static DateTime ParseDateFromTgzName(string fileName)
  {
    try
    {
      // Формат: 080426_122139.tgz
      string name = Path.GetFileNameWithoutExtension(fileName);
      string[] parts = name.Split('_');
      if (parts.Length == 2)
      {
        string datePart = parts[0]; // 080426
        string timePart = parts[1]; // 122139

        int day = int.Parse(datePart.Substring(0, 2));
        int month = int.Parse(datePart.Substring(2, 2));
        int year = int.Parse(datePart.Substring(4, 2)) + 2000;

        int hour = int.Parse(timePart.Substring(0, 2));
        int minute = int.Parse(timePart.Substring(2, 2));
        int second = int.Parse(timePart.Substring(4, 2));

        return new DateTime(year, month, day, hour, minute, second);
      }
    }
    catch { }
    return DateTime.MinValue;
  }

  static async Task MergeLogFiles(List<LogFile> files, string downloadDir)
  {
    Console.WriteLine("\n  [+] Объединение файлов в хронологическом порядке...");

    // Сортируем по дате
    var sortedFiles = files.OrderBy(f => f.DisplayDate).ToList();

    StringBuilder mergedContent = new StringBuilder();
    int successCount = 0;
    int errorCount = 0;

    for (int i = 0; i < sortedFiles.Count; i++)
    {
      var file = sortedFiles[i];
      string dateStr = file.DisplayDate.ToString("dd.MM.yy HH:mm:ss");

      Console.Write($"\n  [{i + 1}/{sortedFiles.Count}] {file.Name} ({dateStr})... ");

      try
      {
        string content = "";

        if (file.Name.EndsWith(".tgz"))
        {
          // Читаем tgz, распаковываем в памяти
          byte[] tgzData = File.ReadAllBytes(file.FullPath);
          content = ExtractTgzContent(tgzData);
        }
        else
        {
          // Читаем app.log напрямую
          content = File.ReadAllText(file.FullPath, Encoding.UTF8);
        }

        if (!string.IsNullOrEmpty(content))
        {
          mergedContent.Append(content);
          if (!content.EndsWith("\n"))
            mergedContent.Append("\n");

          Console.ForegroundColor = ConsoleColor.Green;
          Console.Write("OK");
          Console.ResetColor();
          successCount++;
        }
        else
        {
          Console.ForegroundColor = ConsoleColor.Yellow;
          Console.Write("ПУСТО");
          Console.ResetColor();
        }
      }
      catch (Exception ex)
      {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("ОШИБКА");
        Console.ResetColor();
        Console.Write($" ({ex.Message})");
        errorCount++;
        mergedContent.AppendLine($"[ОШИБКА: файл {file.Name} поврежден]");
      }

      // Рисуем прогресс
      int percent = ((i + 1) * 100) / sortedFiles.Count;
      int barWidth = 50;
      int filledWidth = (int)(barWidth * percent / 100.0);
      Console.Write($" [{new string('#', filledWidth)}{new string('.', barWidth - filledWidth)}] {percent}%");
    }

    Console.WriteLine();

    // Записываем объединенный файл
    if (mergedContent.Length > 0)
    {
      DateTime now = DateTime.Now;
      string timestamp = now.ToString("dd-MM-yy_HH-mm-ss");
      string mergedFileName = $"app.log_{timestamp}";
      string mergedFilePath = Path.Combine(downloadDir, mergedFileName);

      Console.Write($"\n  Запись итогового файла... ");
      File.WriteAllText(mergedFilePath, mergedContent.ToString(), Encoding.UTF8);
      Console.ForegroundColor = ConsoleColor.Green;
      Console.WriteLine("OK");
      Console.ResetColor();

      // Удаляем исходные файлы
      Console.Write($"  Удаление исходных файлов... ");
      foreach (var file in sortedFiles)
      {
        if (File.Exists(file.FullPath))
        {
          File.Delete(file.FullPath);
        }
      }
      Console.ForegroundColor = ConsoleColor.Green;
      Console.WriteLine("OK");
      Console.ResetColor();

      Console.WriteLine($"\n  [+] Объединение завершено!");
      Console.WriteLine($"      Обработано: {successCount} из {sortedFiles.Count} файлов");
      if (errorCount > 0)
        Console.WriteLine($"      Ошибок: {errorCount}");
      Console.WriteLine($"      Итоговый файл: {downloadDir}/{mergedFileName}");
    }
    else
    {
      Console.WriteLine("\n  [!] Нет данных для объединения");
    }
  }

  static string ExtractTgzContent(byte[] tgzData)
  {
    using (var memoryStream = new MemoryStream(tgzData))
    {
      using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
      {
        using (var tarStream = new MemoryStream())
        {
          gzipStream.CopyTo(tarStream);
          tarStream.Seek(0, SeekOrigin.Begin);

          // Читаем tar (упрощенно - ищем файл внутри)
          byte[] buffer = new byte[512];
          List<byte> fileContent = new List<byte>();

          while (tarStream.Position < tarStream.Length)
          {
            tarStream.Read(buffer, 0, 512);

            // Проверяем, что это начало файла (не пустой блок)
            if (buffer[0] == 0)
              break;

            // Получаем размер файла из tar заголовка (позиция 124, длина 12)
            string sizeStr = Encoding.ASCII.GetString(buffer, 124, 11).Trim('\0');
            if (long.TryParse(sizeStr, out long fileSize) && fileSize > 0)
            {
              // Читаем содержимое файла
              int blocks = (int)((fileSize + 511) / 512);
              for (int i = 0; i < blocks; i++)
              {
                tarStream.Read(buffer, 0, 512);
                int bytesToTake = (int)Math.Min(fileSize - fileContent.Count, 512);
                fileContent.AddRange(buffer.Take(bytesToTake));
              }
              break;
            }
          }

          return Encoding.UTF8.GetString(fileContent.ToArray());
        }
      }
    }
  }

  static async Task<List<string>> GetFileList(string dirpath)
  {
    List<string> files = new List<string>();

    try
    {
      using (TcpClient client = new TcpClient())
      {
        await client.ConnectAsync(ServerIp, ServerPort);

        using (NetworkStream stream = client.GetStream())
        {
          string request = string.IsNullOrEmpty(dirpath) ? "DIR\n" : $"DIR {dirpath}\n";
          byte[] requestBytes = Encoding.UTF8.GetBytes(request);
          await stream.WriteAsync(requestBytes, 0, requestBytes.Length);

          byte[] buffer = new byte[1024];
          int bytesRead = await ReadLineAsync(stream, buffer);
          if (bytesRead <= 0) return files;

          string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
          if (!response.StartsWith("START")) return files;

          string[] parts = response.Split(' ');
          if (parts.Length != 2 || !int.TryParse(parts[1], out int totalPackets)) return files;

          string confirm = $"OK {totalPackets}\n";
          byte[] confirmBytes = Encoding.UTF8.GetBytes(confirm);
          await stream.WriteAsync(confirmBytes, 0, confirmBytes.Length);

          using (MemoryStream dataStream = new MemoryStream())
          {
            int receivedPackets = 0;
            while (receivedPackets < totalPackets)
            {
              bytesRead = await ReadLineAsync(stream, buffer);
              if (bytesRead <= 0) break;

              string packetHeader = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
              if (packetHeader == "END") break;

              parts = packetHeader.Split(' ');
              if (parts.Length != 3 || parts[0] != "PACKET" ||
                  !int.TryParse(parts[1], out int packetNum) ||
                  !int.TryParse(parts[2], out int packetSize)) break;

              byte[] packetData = new byte[packetSize];
              int totalRead = 0;
              while (totalRead < packetSize)
              {
                bytesRead = await stream.ReadAsync(packetData, totalRead, packetSize - totalRead);
                if (bytesRead <= 0) break;
                totalRead += bytesRead;
              }

              await dataStream.WriteAsync(packetData, 0, packetSize);

              string packetConfirm = $"OK {packetNum} {packetSize}\n";
              byte[] confirmPacketBytes = Encoding.UTF8.GetBytes(packetConfirm);
              await stream.WriteAsync(confirmPacketBytes, 0, confirmPacketBytes.Length);

              receivedPackets++;
            }

            if (receivedPackets == totalPackets)
            {
              bytesRead = await ReadLineAsync(stream, buffer);
              if (bytesRead > 0)
              {
                string endMsg = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                if (endMsg == "END")
                {
                  dataStream.Seek(0, SeekOrigin.Begin);
                  byte[] receivedData = dataStream.ToArray();
                  string textOutput = Encoding.UTF8.GetString(receivedData);

                  string[] lines = textOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                  foreach (string line in lines)
                  {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] parts2 = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts2.Length >= 2)
                    {
                      string name = string.Join(" ", parts2, 1, parts2.Length - 1);
                      if (name.EndsWith("/"))
                      {
                        name = name.TrimEnd('/');
                      }
                      else if (name.EndsWith("*"))
                      {
                        name = name.TrimEnd('*');
                      }
                      else if (name.EndsWith("@"))
                      {
                        name = name.TrimEnd('@');
                      }
                      files.Add(name);
                    }
                  }
                }
              }
            }
          }
        }
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"\n  Ошибка получения списка файлов: {ex.Message}");
    }

    return files;
  }

  static async Task ChangeServer()
  {
    Console.Clear();

    List<string> menuItems = new List<string>();
    foreach (string ip in ipHistory)
    {
      menuItems.Add(ip);
    }
    menuItems.Add("[Ввести новый IP-адрес]");

    int selectedIndex = 0;
    int scrollOffset = 0;
    int visibleItems = 20;
    bool exitMenu = false;
    string newIp = null;

    while (!exitMenu)
    {
      Console.Clear();

      int windowWidth = Console.WindowWidth - 1;
      string topLine = "+" + new string('-', windowWidth - 2) + "+";
      string title = "СМЕНА СЕРВЕРА";
      int padding = (windowWidth - 2 - title.Length) / 2;
      if (padding < 0) padding = 0;
      string titleLine = "|" + new string(' ', padding) + title + new string(' ', windowWidth - 2 - padding - title.Length) + "|";

      Console.WriteLine(topLine);
      Console.WriteLine(titleLine);
      Console.WriteLine(topLine);
      Console.WriteLine();
      Console.WriteLine("  Используйте [↑] [↓] для навигации, [Enter] для выбора, [Esc] для отмены");
      Console.WriteLine($"  Всего сохранено адресов: {ipHistory.Count}/40");
      Console.WriteLine();

      if (selectedIndex < scrollOffset)
      {
        scrollOffset = selectedIndex;
      }
      else if (selectedIndex >= scrollOffset + visibleItems)
      {
        scrollOffset = selectedIndex - visibleItems + 1;
      }

      for (int i = 0; i < visibleItems && (scrollOffset + i) < menuItems.Count; i++)
      {
        int itemIndex = scrollOffset + i;
        string displayText = menuItems[itemIndex];

        if (itemIndex == selectedIndex)
        {
          Console.ForegroundColor = ConsoleColor.Green;
          Console.BackgroundColor = ConsoleColor.DarkGray;
          Console.Write("  >> ");
          Console.Write(displayText);
          Console.ResetColor();
          Console.WriteLine();
        }
        else
        {
          Console.Write("     ");
          Console.WriteLine(displayText);
        }
      }

      if (menuItems.Count > visibleItems)
      {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;

        if (scrollOffset > 0)
        {
          Console.Write("     ↑ еще " + scrollOffset + " адресов выше ↑");
        }

        if (scrollOffset + visibleItems < menuItems.Count)
        {
          if (scrollOffset > 0) Console.Write("   ");
          Console.Write("↓ еще " + (menuItems.Count - (scrollOffset + visibleItems)) + " адресов ниже ↓");
        }

        Console.ResetColor();
      }

      Console.WriteLine();
      Console.WriteLine("  Текущий сервер: {0}:{1}", ServerIp, ServerPort);

      ConsoleKeyInfo key = Console.ReadKey(true);

      switch (key.Key)
      {
        case ConsoleKey.UpArrow:
          if (selectedIndex > 0)
          {
            selectedIndex--;
          }
          else
          {
            selectedIndex = menuItems.Count - 1;
            scrollOffset = Math.Max(0, menuItems.Count - visibleItems);
          }
          break;
        case ConsoleKey.DownArrow:
          if (selectedIndex < menuItems.Count - 1)
          {
            selectedIndex++;
          }
          else
          {
            selectedIndex = 0;
            scrollOffset = 0;
          }
          break;
        case ConsoleKey.Escape:
          return;
        case ConsoleKey.Enter:
          if (selectedIndex < ipHistory.Count)
          {
            newIp = ipHistory[selectedIndex];
            if (await TestConnection(newIp))
            {
              ServerIp = newIp;
              SaveIpToHistory(ServerIp);
              Console.WriteLine("\n  [+] Сервер изменен на {0}:{1}", ServerIp, ServerPort);
              Console.WriteLine("\n  Нажмите любую клавишу для продолжения...");
              Console.ReadKey(true);
              return;
            }
            else
            {
              Console.WriteLine("\n  [!] Не удалось подключиться к серверу {0}:{1}", newIp, ServerPort);
              Console.WriteLine("\n  Нажмите любую клавишу для продолжения...");
              Console.ReadKey(true);
            }
          }
          else
          {
            string ip = await EnterNewIp();
            if (ip != null)
            {
              ServerIp = ip;
              SaveIpToHistory(ServerIp);
              Console.WriteLine("\n  [+] Сервер изменен на {0}:{1}", ServerIp, ServerPort);
              Console.WriteLine("\n  Нажмите любую клавишу для продолжения...");
              Console.ReadKey(true);
              return;
            }
          }
          break;
      }
    }
  }

  static async Task HandleNetTestCommand()
  {
    Console.Clear();

    int windowWidth = Console.WindowWidth - 1;
    string topLine = "+" + new string('-', windowWidth - 2) + "+";
    string title = "СЕТЕВОЙ ТЕСТ";
    int padding = (windowWidth - 2 - title.Length) / 2;
    if (padding < 0) padding = 0;
    string titleLine = "|" + new string(' ', padding) + title + new string(' ', windowWidth - 2 - padding - title.Length) + "|";

    Console.WriteLine(topLine);
    Console.WriteLine(titleLine);
    Console.WriteLine(topLine);
    Console.WriteLine();
    Console.WriteLine("  Esc - выход в главное меню");
    Console.WriteLine();
    Console.Write("  Введите интервал в минутах (1-60): ");

    string input = await ReadLineWithEscapeAsync();
    if (input == null) return;

    if (string.IsNullOrWhiteSpace(input))
    {
      Console.WriteLine("\n  [!] Интервал не может быть пустым!");
      Console.WriteLine("\n  Нажмите любую клавишу для продолжения...");
      Console.ReadKey(true);
      return;
    }

    if (!int.TryParse(input, out int intervalMinutes) || intervalMinutes < 1 || intervalMinutes > 60)
    {
      Console.WriteLine("\n  [!] Введите корректное число от 1 до 60 минут!");
      Console.WriteLine("\n  Нажмите любую клавишу для продолжения...");
      Console.ReadKey(true);
      return;
    }

    try
    {
      using (TcpClient client = new TcpClient())
      {
        Console.WriteLine($"\n  Подключение к {ServerIp}:{ServerPort}...");
        await client.ConnectAsync(ServerIp, ServerPort);

        using (NetworkStream stream = client.GetStream())
        {
          string request = $"NET_TEST={intervalMinutes}\n";
          byte[] requestBytes = Encoding.UTF8.GetBytes(request);
          await stream.WriteAsync(requestBytes, 0, requestBytes.Length);
          Console.WriteLine($"  Запрос NET_TEST={intervalMinutes} отправлен");

          // Читаем ответ (простая строка, не через пакетную передачу)
          byte[] buffer = new byte[1024];
          int bytesRead = await ReadLineAsync(stream, buffer);

          if (bytesRead > 0)
          {
            string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

            Console.WriteLine();
            Console.WriteLine("  +-----------------------------------------------------+");
            Console.WriteLine("  |  РЕЗУЛЬТАТ ТЕСТА                                    |");
            Console.WriteLine("  +-----------------------------------------------------+");

            if (response.StartsWith("NET_RUNNING:"))
            {
              Console.ForegroundColor = ConsoleColor.Yellow;
              Console.WriteLine($"\n  {response}");
              Console.ResetColor();
              Console.WriteLine("\n  Тест уже запущен!");
            }
            else if (response.StartsWith("NET_RUN interval="))
            {
              Console.ForegroundColor = ConsoleColor.Green;
              Console.WriteLine($"\n  {response}");
              Console.ResetColor();
              Console.WriteLine($"\n  Тест успешно запущен с интервалом {intervalMinutes} минут!");
            }
            else if (response.StartsWith("ERROR"))
            {
              Console.ForegroundColor = ConsoleColor.Red;
              Console.WriteLine($"\n  {response}");
              Console.ResetColor();
            }
            else
            {
              Console.WriteLine($"\n  Неожиданный ответ: {response}");
            }

            Console.WriteLine("  +-----------------------------------------------------+");
          }
          else
          {
            Console.WriteLine("\n  [!] Сервер не вернул ответ");
          }
        }
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"\n  Ошибка: {ex.Message}");
    }

    Console.WriteLine("\n  Нажмите любую клавишу для продолжения...");
    Console.ReadKey(true);
  }
  static async Task HandleCmdCommand()
  {
    historyIndex = -1;
    currentInput = "";

    Console.WriteLine();
    Console.WriteLine("  Esc - выход в главное меню | ↑/↓ - история команд");
    Console.WriteLine();

    while (true)
    {
      Console.Write("  Введите команду: ");

      string command = await ReadLineWithHistoryForCommandAsync();
      if (command == null) return;

      if (string.IsNullOrEmpty(command))
      {
        continue;
      }

      SaveCommandToHistory(command);

      try
      {
        using (TcpClient client = new TcpClient())
        {
          Console.WriteLine($"\n  Подключение к {ServerIp}:{ServerPort}...");
          await client.ConnectAsync(ServerIp, ServerPort);

          using (NetworkStream stream = client.GetStream())
          {
            string request = $"CMD {command}\n";
            byte[] requestBytes = Encoding.UTF8.GetBytes(request);
            await stream.WriteAsync(requestBytes, 0, requestBytes.Length);

            await ReceiveData(stream, true, true);
          }
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"\n  Ошибка: {ex.Message}");
      }

      Console.WriteLine();
    }
  }

  static async Task HandleDirCommand()
  {
    while (true)
    {
      Console.Clear();

      int windowWidth = Console.WindowWidth - 1;
      string topLine = "+" + new string('-', windowWidth - 2) + "+";
      string title = "ПРОСМОТР СОДЕРЖИМОГО";
      int padding = (windowWidth - 2 - title.Length) / 2;
      if (padding < 0) padding = 0;
      string titleLine = "|" + new string(' ', padding) + title + new string(' ', windowWidth - 2 - padding - title.Length) + "|";

      Console.WriteLine(topLine);
      Console.WriteLine(titleLine);
      Console.WriteLine(topLine);
      Console.WriteLine();
      Console.WriteLine("  (Esc для возврата в главное меню)");
      Console.WriteLine();
      Console.Write("  Введите путь к директории (или нажмите Enter для текущей): ");

      string dirpath = await ReadLineWithEscapeAsync();
      if (dirpath == null) return;

      try
      {
        using (TcpClient client = new TcpClient())
        {
          Console.WriteLine($"\n  Подключение к серверу {ServerIp}:{ServerPort}...");
          await client.ConnectAsync(ServerIp, ServerPort);
          Console.WriteLine("  Подключение установлено");

          using (NetworkStream stream = client.GetStream())
          {
            string request = string.IsNullOrEmpty(dirpath) ? "DIR\n" : $"DIR {dirpath}\n";
            byte[] requestBytes = Encoding.UTF8.GetBytes(request);
            await stream.WriteAsync(requestBytes, 0, requestBytes.Length);
            Console.WriteLine($"  Запрос DIR отправлен: {(string.IsNullOrEmpty(dirpath) ? "текущая директория" : dirpath)}");

            await ReceiveData(stream, true, false);
          }
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"\n  Ошибка: {ex.Message}");
      }

      Console.WriteLine("\n  Нажмите Esc для возврата в меню или любую клавишу для повтора...");
      var exitKey = Console.ReadKey(true);
      if (exitKey.Key == ConsoleKey.Escape)
        return;
    }
  }

  static async Task HandleFileDownload()
  {
    historyIndex = -1;
    currentInput = "";

    while (true)
    {
      Console.Clear();

      int windowWidth = Console.WindowWidth - 1;
      string topLine = "+" + new string('-', windowWidth - 2) + "+";
      string title = "СКАЧИВАНИЕ ФАЙЛА";
      int padding = (windowWidth - 2 - title.Length) / 2;
      if (padding < 0) padding = 0;
      string titleLine = "|" + new string(' ', padding) + title + new string(' ', windowWidth - 2 - padding - title.Length) + "|";

      Console.WriteLine(topLine);
      Console.WriteLine(titleLine);
      Console.WriteLine(topLine);
      Console.WriteLine();
      Console.WriteLine("  (Esc для возврата в главное меню)");
      Console.WriteLine("  (↑/↓ для навигации по истории путей)");
      Console.WriteLine();
      Console.Write("  Введите путь к файлу: ");

      string filepath = await ReadLineWithHistoryAsync();
      if (filepath == null) return;

      if (string.IsNullOrEmpty(filepath))
      {
        Console.WriteLine("  Путь к файлу не может быть пустым");
        Console.WriteLine("\n  Нажмите Esc для возврата в меню или любую клавишу для повтора...");
        var key = Console.ReadKey(true);
        if (key.Key == ConsoleKey.Escape)
          return;
        continue;
      }

      SaveCommandToHistory(filepath);
      lastRequestedFile = filepath;

      try
      {
        using (TcpClient client = new TcpClient())
        {
          Console.WriteLine($"\n  Подключение к серверу {ServerIp}:{ServerPort}...");
          await client.ConnectAsync(ServerIp, ServerPort);
          Console.WriteLine("  Подключение установлено");

          using (NetworkStream stream = client.GetStream())
          {
            string request = $"{filepath}\n";
            byte[] requestBytes = Encoding.UTF8.GetBytes(request);
            await stream.WriteAsync(requestBytes, 0, requestBytes.Length);
            Console.WriteLine($"  Запрос FILE отправлен: {filepath}");

            await ReceiveData(stream, false, false);
          }
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"\n  Ошибка: {ex.Message}");
      }

      if (!string.IsNullOrEmpty(lastDownloadedFile) && File.Exists(lastDownloadedFile))
      {
        Console.WriteLine("\n  +-----------------------------------------------------+");
        Console.WriteLine("  |  Файл успешно скачан!                               |");
        Console.WriteLine($"  |  Имя файла: {Path.GetFileName(lastDownloadedFile)}");
        Console.WriteLine($"  |  Размер: {FormatBytes(new FileInfo(lastDownloadedFile).Length)}");
        Console.WriteLine("  +-----------------------------------------------------+");
      }

      Console.WriteLine("\n  Нажмите Esc для возврата в меню или любую клавишу для повтора...");
      var exitKey = Console.ReadKey(true);
      if (exitKey.Key == ConsoleKey.Escape)
        return;
    }
  }

  static async Task<string> ReadLineWithHistoryAsync()
  {
    string input = currentInput;
    int cursorPos = input.Length;
    historyIndex = -1;
    int promptLength = "  Введите путь к файлу: ".Length;

    Console.Write(input);

    while (true)
    {
      if (Console.KeyAvailable)
      {
        var key = Console.ReadKey(true);

        if (key.Key == ConsoleKey.UpArrow)
        {
          if (historyIndex < commandHistory.Count - 1)
          {
            historyIndex++;
            if (historyIndex == 0)
            {
              currentInput = input;
            }
            input = commandHistory[historyIndex];
            cursorPos = input.Length;

            Console.CursorLeft = promptLength;
            Console.Write(new string(' ', Console.WindowWidth - promptLength - 1));
            Console.CursorLeft = promptLength;
            Console.Write(input);
            Console.CursorLeft = promptLength + cursorPos;
          }
          else
          {
            Console.Beep();
          }
        }
        else if (key.Key == ConsoleKey.DownArrow)
        {
          if (historyIndex > 0)
          {
            historyIndex--;
            input = commandHistory[historyIndex];
            cursorPos = input.Length;

            Console.CursorLeft = promptLength;
            Console.Write(new string(' ', Console.WindowWidth - promptLength - 1));
            Console.CursorLeft = promptLength;
            Console.Write(input);
            Console.CursorLeft = promptLength + cursorPos;
          }
          else if (historyIndex == 0)
          {
            historyIndex = -1;
            input = currentInput;
            cursorPos = input.Length;

            Console.CursorLeft = promptLength;
            Console.Write(new string(' ', Console.WindowWidth - promptLength - 1));
            Console.CursorLeft = promptLength;
            Console.Write(input);
            Console.CursorLeft = promptLength + cursorPos;
          }
          else
          {
            Console.Beep();
          }
        }
        else if (key.Key == ConsoleKey.Escape)
        {
          return null;
        }
        else if (key.Key == ConsoleKey.Enter)
        {
          Console.WriteLine();
          currentInput = "";
          return input;
        }
        else if (key.Key == ConsoleKey.Backspace && cursorPos > 0)
        {
          input = input.Remove(cursorPos - 1, 1);
          cursorPos--;

          Console.CursorLeft = promptLength;
          Console.Write(input + " ");
          Console.CursorLeft = promptLength + cursorPos;
        }
        else if (key.Key == ConsoleKey.LeftArrow && cursorPos > 0)
        {
          cursorPos--;
          Console.CursorLeft = promptLength + cursorPos;
        }
        else if (key.Key == ConsoleKey.RightArrow && cursorPos < input.Length)
        {
          cursorPos++;
          Console.CursorLeft = promptLength + cursorPos;
        }
        else if (key.Key == ConsoleKey.Home)
        {
          cursorPos = 0;
          Console.CursorLeft = promptLength;
        }
        else if (key.Key == ConsoleKey.End)
        {
          cursorPos = input.Length;
          Console.CursorLeft = promptLength + cursorPos;
        }
        else if (!char.IsControl(key.KeyChar))
        {
          input = input.Insert(cursorPos, key.KeyChar.ToString());
          cursorPos++;

          Console.CursorLeft = promptLength;
          Console.Write(input);
          Console.CursorLeft = promptLength + cursorPos;
        }
      }
      await Task.Delay(10);
    }
  }

  static async Task<string> ReadLineWithHistoryForCommandAsync()
  {
    string input = "";
    int cursorPos = 0;
    historyIndex = -1;
    currentInput = "";
    int promptLength = "  Введите команду: ".Length;

    while (true)
    {
      if (Console.KeyAvailable)
      {
        var key = Console.ReadKey(true);

        if (key.Key == ConsoleKey.UpArrow)
        {
          if (historyIndex < commandHistory.Count - 1)
          {
            historyIndex++;
            if (historyIndex == 0 && string.IsNullOrEmpty(currentInput))
            {
              currentInput = input;
            }
            input = commandHistory[historyIndex];
            cursorPos = input.Length;

            Console.CursorLeft = promptLength;
            Console.Write(new string(' ', Console.WindowWidth - promptLength - 1));
            Console.CursorLeft = promptLength;
            Console.Write(input);
            Console.CursorLeft = promptLength + cursorPos;
          }
        }
        else if (key.Key == ConsoleKey.DownArrow)
        {
          if (historyIndex > 0)
          {
            historyIndex--;
            input = commandHistory[historyIndex];
            cursorPos = input.Length;

            Console.CursorLeft = promptLength;
            Console.Write(new string(' ', Console.WindowWidth - promptLength - 1));
            Console.CursorLeft = promptLength;
            Console.Write(input);
            Console.CursorLeft = promptLength + cursorPos;
          }
          else if (historyIndex == 0)
          {
            historyIndex = -1;
            input = currentInput;
            cursorPos = input.Length;

            Console.CursorLeft = promptLength;
            Console.Write(new string(' ', Console.WindowWidth - promptLength - 1));
            Console.CursorLeft = promptLength;
            Console.Write(input);
            Console.CursorLeft = promptLength + cursorPos;
          }
        }
        else if (key.Key == ConsoleKey.Escape)
        {
          Console.WriteLine();
          return null;
        }
        else if (key.Key == ConsoleKey.Enter)
        {
          Console.WriteLine();
          return input;
        }
        else if (key.Key == ConsoleKey.Backspace && cursorPos > 0)
        {
          input = input.Remove(cursorPos - 1, 1);
          cursorPos--;

          Console.CursorLeft = promptLength;
          Console.Write(input + " ");
          Console.CursorLeft = promptLength + cursorPos;
        }
        else if (key.Key == ConsoleKey.LeftArrow && cursorPos > 0)
        {
          cursorPos--;
          Console.CursorLeft = promptLength + cursorPos;
        }
        else if (key.Key == ConsoleKey.RightArrow && cursorPos < input.Length)
        {
          cursorPos++;
          Console.CursorLeft = promptLength + cursorPos;
        }
        else if (key.Key == ConsoleKey.Home)
        {
          cursorPos = 0;
          Console.CursorLeft = promptLength;
        }
        else if (key.Key == ConsoleKey.End)
        {
          cursorPos = input.Length;
          Console.CursorLeft = promptLength + cursorPos;
        }
        else if (!char.IsControl(key.KeyChar))
        {
          input = input.Insert(cursorPos, key.KeyChar.ToString());
          cursorPos++;

          Console.CursorLeft = promptLength;
          Console.Write(input);
          Console.CursorLeft = promptLength + cursorPos;
        }
      }
      await Task.Delay(10);
    }
  }

  static async Task<string> ReadLineWithEscapeAsync()
  {
    string input = "";
    while (true)
    {
      if (Console.KeyAvailable)
      {
        var key = Console.ReadKey(true);
        if (key.Key == ConsoleKey.Escape)
        {
          return null;
        }
        else if (key.Key == ConsoleKey.Enter)
        {
          Console.WriteLine();
          return input;
        }
        else if (key.Key == ConsoleKey.Backspace && input.Length > 0)
        {
          input = input.Substring(0, input.Length - 1);
          Console.Write("\b \b");
        }
        else if (!char.IsControl(key.KeyChar))
        {
          input += key.KeyChar;
          Console.Write(key.KeyChar);
        }
      }
      await Task.Delay(10);
    }
  }

  static async Task ReceiveData(NetworkStream stream, bool isTextOutput, bool isCommand)
  {
    byte[] buffer = new byte[1024];

    int bytesRead = await ReadLineAsync(stream, buffer);
    if (bytesRead <= 0)
    {
      Console.WriteLine("  Ошибка: соединение разорвано");
      return;
    }

    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

    if (response.StartsWith("ERROR"))
    {
      Console.WriteLine($"  Ошибка сервера: {response}");
      return;
    }

    if (!response.StartsWith("START"))
    {
      Console.WriteLine($"  Неожиданный ответ: {response}");
      return;
    }

    string[] parts = response.Split(' ');
    if (parts.Length != 2 || !int.TryParse(parts[1], out int totalPackets))
    {
      Console.WriteLine($"  Неверный формат START: {response}");
      return;
    }

    long totalBytes = totalPackets * PacketSize;

    Console.WriteLine($"\n  Получен START: всего пакетов {totalPackets}, размер ~{FormatBytes(totalBytes)}");

    string confirm = $"OK {totalPackets}\n";
    byte[] confirmBytes = Encoding.UTF8.GetBytes(confirm);
    await stream.WriteAsync(confirmBytes, 0, confirmBytes.Length);

    using (MemoryStream dataStream = new MemoryStream())
    {
      int receivedPackets = 0;
      bool success = true;

      int lastPercent = -1;
      DateTime startTime = DateTime.Now;
      long totalBytesReceived = 0;

      Console.WriteLine();

      while (receivedPackets < totalPackets)
      {
        bytesRead = await ReadLineAsync(stream, buffer);
        if (bytesRead <= 0)
        {
          Console.WriteLine("\n  Ошибка: соединение разорвано");
          success = false;
          break;
        }

        string packetHeader = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

        if (packetHeader == "END")
        {
          break;
        }

        parts = packetHeader.Split(' ');
        if (parts.Length != 3 || parts[0] != "PACKET" ||
            !int.TryParse(parts[1], out int packetNum) ||
            !int.TryParse(parts[2], out int packetSize))
        {
          Console.WriteLine($"\n  Неверный формат пакета: {packetHeader}");
          success = false;
          break;
        }

        if (packetNum != receivedPackets + 1)
        {
          Console.WriteLine($"\n  Ошибка: ожидался пакет {receivedPackets + 1}, получен {packetNum}");
          success = false;
          break;
        }

        byte[] packetData = new byte[packetSize];
        int totalRead = 0;

        while (totalRead < packetSize)
        {
          bytesRead = await stream.ReadAsync(packetData, totalRead, packetSize - totalRead);
          if (bytesRead <= 0)
          {
            Console.WriteLine("\n  Ошибка чтения данных пакета");
            success = false;
            break;
          }
          totalRead += bytesRead;
        }

        if (!success) break;

        await dataStream.WriteAsync(packetData, 0, packetSize);
        totalBytesReceived += packetSize;

        string packetConfirm = $"OK {packetNum} {packetSize}\n";
        byte[] confirmPacketBytes = Encoding.UTF8.GetBytes(packetConfirm);
        await stream.WriteAsync(confirmPacketBytes, 0, confirmPacketBytes.Length);

        receivedPackets++;

        int currentPercent = (receivedPackets * 100) / totalPackets;
        if (currentPercent != lastPercent)
        {
          lastPercent = currentPercent;
          DrawProgressBar(currentPercent, receivedPackets, totalPackets);
        }
      }

      Console.WriteLine();

      if (success && receivedPackets == totalPackets)
      {
        bytesRead = await ReadLineAsync(stream, buffer);
        if (bytesRead > 0)
        {
          string endMsg = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
          if (endMsg == "END")
          {
            TimeSpan duration = DateTime.Now - startTime;
            double speedMBps = totalBytesReceived / duration.TotalSeconds / (1024 * 1024);
            Console.WriteLine($"\n  [+] Передача успешно завершена за {duration.TotalSeconds:F1} сек");
            Console.WriteLine($"    Средняя скорость: {speedMBps:F2} MB/s");

            dataStream.Seek(0, SeekOrigin.Begin);
            byte[] receivedData = dataStream.ToArray();

            if (isTextOutput)
            {
              string textOutput = Encoding.UTF8.GetString(receivedData);

              if (isCommand)
              {
                Console.WriteLine("\n  === РЕЗУЛЬТАТ КОМАНДЫ ===");
                Console.WriteLine(textOutput);
              }
              else
              {
                Console.WriteLine("\n  === СОДЕРЖИМОЕ ДИРЕКТОРИИ ===");
                FormatDirectoryListing(textOutput);
              }
            }
            else
            {
              try
              {
                dataStream.Seek(0, SeekOrigin.Begin);
                string outputFileName = GenerateFileNameFromPath(lastRequestedFile);

                Console.Write("\n  Распаковка файла... ");
                using (var gzipStream = new GZipStream(dataStream, CompressionMode.Decompress))
                using (var outputStream = File.Create(outputFileName))
                {
                  gzipStream.CopyTo(outputStream);
                }
                Console.WriteLine("готово!");

                Console.WriteLine($"\n  [+] Файл успешно скачан и распакован: {outputFileName}");
                lastDownloadedFile = outputFileName;
                FileInfo fileInfo = new FileInfo(outputFileName);
                Console.WriteLine($"    Размер файла: {FormatBytes(fileInfo.Length)}");
              }
              catch (Exception ex)
              {
                Console.WriteLine($"\n  [-] Не удалось распаковать как gzip: {ex.Message}");
                string outputFileName = GenerateFileNameFromPath(lastRequestedFile) + ".bin";
                File.WriteAllBytes(outputFileName, receivedData);
                Console.WriteLine($"  [+] Данные сохранены как: {outputFileName}");
                Console.WriteLine($"    Размер: {FormatBytes(receivedData.Length)}");
                lastDownloadedFile = outputFileName;
              }
            }
          }
        }
      }
      else
      {
        Console.WriteLine($"  [-] Ошибка: получено {receivedPackets} из {totalPackets} пакетов");
      }
    }
  }

  static async Task ReceiveDataSilent(NetworkStream stream, string fileName)
  {
    byte[] buffer = new byte[1024];

    int bytesRead = await ReadLineAsync(stream, buffer);
    if (bytesRead <= 0)
    {
      return;
    }

    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

    if (response.StartsWith("ERROR"))
    {
      Console.WriteLine("ОШИБКА");
      return;
    }

    if (!response.StartsWith("START"))
    {
      return;
    }

    string[] parts = response.Split(' ');
    if (parts.Length != 2 || !int.TryParse(parts[1], out int totalPackets))
    {
      return;
    }

    string confirm = $"OK {totalPackets}\n";
    byte[] confirmBytes = Encoding.UTF8.GetBytes(confirm);
    await stream.WriteAsync(confirmBytes, 0, confirmBytes.Length);

    using (MemoryStream dataStream = new MemoryStream())
    {
      int receivedPackets = 0;
      bool success = true;
      int lastPercent = -1;

      while (receivedPackets < totalPackets)
      {
        bytesRead = await ReadLineAsync(stream, buffer);
        if (bytesRead <= 0)
        {
          success = false;
          break;
        }

        string packetHeader = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

        if (packetHeader == "END")
        {
          break;
        }

        parts = packetHeader.Split(' ');
        if (parts.Length != 3 || parts[0] != "PACKET" ||
            !int.TryParse(parts[1], out int packetNum) ||
            !int.TryParse(parts[2], out int packetSize))
        {
          success = false;
          break;
        }

        byte[] packetData = new byte[packetSize];
        int totalRead = 0;

        while (totalRead < packetSize)
        {
          bytesRead = await stream.ReadAsync(packetData, totalRead, packetSize - totalRead);
          if (bytesRead <= 0)
          {
            success = false;
            break;
          }
          totalRead += bytesRead;
        }

        if (!success) break;

        await dataStream.WriteAsync(packetData, 0, packetSize);

        string packetConfirm = $"OK {packetNum} {packetSize}\n";
        byte[] confirmPacketBytes = Encoding.UTF8.GetBytes(packetConfirm);
        await stream.WriteAsync(confirmPacketBytes, 0, confirmPacketBytes.Length);

        receivedPackets++;

        int currentPercent = (receivedPackets * 100) / totalPackets;
        if (currentPercent != lastPercent)
        {
          lastPercent = currentPercent;

          int currentLeft = Console.CursorLeft;
          int currentTop = Console.CursorTop;

          Console.CursorLeft = 0;
          DrawProgressBarInline(currentPercent, receivedPackets, totalPackets);

          Console.CursorLeft = currentLeft;
          Console.CursorTop = currentTop;
        }
      }

      if (success && receivedPackets == totalPackets)
      {
        bytesRead = await ReadLineAsync(stream, buffer);
        if (bytesRead > 0)
        {
          string endMsg = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
          if (endMsg == "END")
          {
            dataStream.Seek(0, SeekOrigin.Begin);
            byte[] receivedData = dataStream.ToArray();

            try
            {
              dataStream.Seek(0, SeekOrigin.Begin);
              string outputFileName = GenerateFileNameFromPath(lastRequestedFile);

              using (var gzipStream = new GZipStream(dataStream, CompressionMode.Decompress))
              using (var outputStream = File.Create(outputFileName))
              {
                gzipStream.CopyTo(outputStream);
              }
              lastDownloadedFile = outputFileName;

              Console.ForegroundColor = ConsoleColor.Green;
              Console.Write("OK");
              Console.ResetColor();
              Console.WriteLine();
            }
            catch
            {
              string outputFileName = GenerateFileNameFromPath(lastRequestedFile) + ".bin";
              File.WriteAllBytes(outputFileName, receivedData);
              lastDownloadedFile = outputFileName;

              Console.ForegroundColor = ConsoleColor.Green;
              Console.Write("OK");
              Console.ResetColor();
              Console.WriteLine();
            }
          }
        }
      }
      else
      {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("ОШИБКА");
        Console.ResetColor();
        Console.WriteLine();
      }
    }
  }

  static void DrawProgressBar(int percent, int current, int total)
  {
    int windowWidth = Console.WindowWidth;
    int barWidth = windowWidth - 25;
    if (barWidth < 20) barWidth = 50;

    int filledWidth = (int)(barWidth * percent / 100.0);
    int emptyWidth = barWidth - filledWidth;

    Console.Write("\r" + new string(' ', windowWidth) + "\r");

    Console.Write("  [");

    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write(new string('#', filledWidth));
    Console.ResetColor();

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write(new string('.', emptyWidth));
    Console.ResetColor();

    Console.Write($"] {percent,3}% ({current}/{total})");
  }

  static void DrawProgressBarInline(int percent, int current, int total)
  {
    int windowWidth = Console.WindowWidth;
    int barWidth = windowWidth - 30;
    if (barWidth < 20) barWidth = 50;

    int filledWidth = (int)(barWidth * percent / 100.0);
    int emptyWidth = barWidth - filledWidth;

    Console.Write("\r");
    Console.Write(new string(' ', windowWidth - 1));
    Console.Write("\r");

    Console.Write("  ");

    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write(new string('#', filledWidth));
    Console.ResetColor();

    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.Write(new string('.', emptyWidth));
    Console.ResetColor();

    Console.Write($" {percent,3}% ({current}/{total})");
  }

  static string FormatBytes(long bytes)
  {
    string[] sizes = { "B", "KB", "MB", "GB", "TB" };
    double len = bytes;
    int order = 0;
    while (len >= 1024 && order < sizes.Length - 1)
    {
      order++;
      len = len / 1024;
    }
    return $"{len:F1} {sizes[order]}";
  }

  static async Task<int> ReadLineAsync(NetworkStream stream, byte[] buffer)
  {
    int totalRead = 0;

    while (totalRead < buffer.Length)
    {
      int bytesRead = await stream.ReadAsync(buffer, totalRead, 1);
      if (bytesRead == 0)
        return 0;

      if (buffer[totalRead] == '\n')
      {
        totalRead++;
        break;
      }

      totalRead++;
    }

    return totalRead;
  }

  static string GenerateFileNameFromPath(string filePath)
  {
    string fileName = Path.GetFileName(filePath);

    if (string.IsNullOrEmpty(fileName))
    {
      fileName = "downloaded_file";
    }

    DateTime now = DateTime.Now;
    string timestamp = now.ToString("dd_MM_HH_mm_ss");
    string ipFormatted = ServerIp.Replace('.', '_');

    return $"{ipFormatted}_{fileName}-{timestamp}";
  }

  static void FormatDirectoryListing(string textOutput)
  {
    string[] lines = textOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

    List<DirItem> directories = new List<DirItem>();
    List<DirItem> files = new List<DirItem>();

    foreach (string line in lines)
    {
      if (string.IsNullOrWhiteSpace(line))
        continue;

      string[] parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length >= 2)
      {
        string sizeStr = parts[0];
        string name = string.Join(" ", parts, 1, parts.Length - 1);

        DirItem item = new DirItem();

        if (double.TryParse(sizeStr, out double sizeInKB))
        {
          item.Size = sizeInKB;
        }
        else
        {
          item.Size = 0;
        }

        if (name.EndsWith("/"))
        {
          item.Name = name.TrimEnd('/');
          item.Type = ItemType.Directory;
          directories.Add(item);
        }
        else
        {
          item.Name = name;

          if (name.EndsWith("*"))
          {
            item.Name = name.TrimEnd('*');
            item.Type = ItemType.Executable;
          }
          else if (name.EndsWith("@"))
          {
            item.Name = name.TrimEnd('@');
            item.Type = ItemType.Symlink;
          }
          else
          {
            item.Type = ItemType.File;
          }

          files.Add(item);
        }
      }
    }

    directories.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    files.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

    if (directories.Count > 0)
    {
      PrintItemsVertically(directories, true);
    }

    if (files.Count > 0)
    {
      if (directories.Count > 0)
        Console.WriteLine();

      PrintItemsVertically(files, true);
    }
  }

  static void PrintItemsVertically(List<DirItem> items, bool useColors)
  {
    if (items.Count == 0) return;

    if (items[0].Type == ItemType.Directory)
    {
      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.WriteLine("  === ДИРЕКТОРИИ ===");
      Console.ResetColor();
    }
    else
    {
      Console.WriteLine("  === ФАЙЛЫ ===");
    }

    int columns = 4;
    int rows = (int)Math.Ceiling((double)items.Count / columns);

    int[] columnNameWidths = new int[columns];
    int[] columnSizeWidths = new int[columns];

    for (int col = 0; col < columns; col++)
    {
      int maxNameWidth = 0;
      int maxSizeWidth = 0;

      for (int row = 0; row < rows; row++)
      {
        int index = col * rows + row;
        if (index < items.Count)
        {
          string sizeStr = $"{items[index].Size:F1} kb";
          maxNameWidth = Math.Max(maxNameWidth, items[index].Name.Length);
          maxSizeWidth = Math.Max(maxSizeWidth, sizeStr.Length);
        }
      }

      columnNameWidths[col] = maxNameWidth;
      columnSizeWidths[col] = maxSizeWidth;
    }

    for (int row = 0; row < rows; row++)
    {
      Console.Write("  ");
      for (int col = 0; col < columns; col++)
      {
        int index = col * rows + row;
        if (index < items.Count)
        {
          var item = items[index];

          if (useColors)
          {
            if (item.Type == ItemType.Directory)
            {
              Console.ForegroundColor = ConsoleColor.Cyan;
            }
            else if (item.Type == ItemType.Executable)
            {
              Console.ForegroundColor = ConsoleColor.Green;
            }
            else if (item.Type == ItemType.Symlink)
            {
              Console.ForegroundColor = ConsoleColor.DarkYellow;
            }
            else
            {
              Console.ForegroundColor = ConsoleColor.White;
            }
          }

          string sizeStr = $"{item.Size:F1} kb";
          string text = item.Name.PadRight(columnNameWidths[col]) + " " + sizeStr.PadLeft(columnSizeWidths[col]);

          if (col < columns - 1)
          {
            Console.Write(text.PadRight(columnNameWidths[col] + columnSizeWidths[col] + 3));
          }
          else
          {
            Console.Write(text);
          }

          Console.ResetColor();
        }
        else
        {
          if (col < columns - 1)
          {
            Console.Write(new string(' ', columnNameWidths[col] + columnSizeWidths[col] + 3));
          }
        }
      }
      Console.WriteLine();
    }
  }

  class DirItem
  {
    public string Name { get; set; }
    public double Size { get; set; }
    public ItemType Type { get; set; }
  }

  enum ItemType
  {
    Directory,
    File,
    Executable,
    Symlink
  }

  class LogFile
  {
    public string Name { get; set; }
    public bool IsTgz { get; set; }
    public bool Downloaded { get; set; }
    public string FullPath { get; set; }
    public DateTime DisplayDate { get; set; }
  }
}