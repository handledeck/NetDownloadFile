using System;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

class TcpFileClient
{
  private const string ConfigFile = "client_config.txt";
  private const int PacketSize = 512;
  private const int ServerPort = 55555;
  private static string ServerIp = "";
  private static string lastDownloadedFile = "";
  private static string lastRequestedFile = "";
  private static List<string> ipHistory = new List<string>();

  static async Task Main(string[] args)
  {
    Console.OutputEncoding = Encoding.UTF8;
    Console.Title = "TCP файловый клиент";

    // Загружаем историю IP адресов
    LoadIpHistory();

    // Выбираем IP адрес
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

    // Сохраняем успешный IP в историю
    SaveIpToHistory(ServerIp);

    // Устанавливаем размер окна консоли для лучшего отображения
    try
    {
      Console.WindowWidth = 120;
      Console.BufferWidth = 120;
      Console.WindowHeight = 40;
    }
    catch { }

    await MainMenu();
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

      // Удаляем старый entry если есть
      if (ipHistory.Contains(ip))
      {
        ipHistory.Remove(ip);
      }
      // Добавляем в начало
      ipHistory.Insert(0, ip);

      // Сохраняем только последние 40 адресов (было 10)
      while (ipHistory.Count > 40)
      {
        ipHistory.RemoveAt(ipHistory.Count - 1);
      }

      // Записываем в файл
      File.WriteAllLines(ConfigFile, ipHistory);
    }
    catch { }
  }


  static bool IsValidIp(string ip)
  {
    if (string.IsNullOrWhiteSpace(ip)) return false;

    // Проверка формата IPv4
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
    // Если история пуста, сразу переходим к вводу
    if (ipHistory.Count == 0)
    {
      return await EnterNewIp();
    }

    // Создаем список для меню
    List<string> menuItems = new List<string>();
    foreach (string ip in ipHistory)
    {
      menuItems.Add(ip);
    }
    menuItems.Add("[Ввести новый IP-адрес]");

    int selectedIndex = 0;
    int scrollOffset = 0; // Смещение прокрутки
    int visibleItems = 20; // Количество видимых пунктов
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

      // Корректируем scrollOffset чтобы выбранный элемент был в видимой области
      if (selectedIndex < scrollOffset)
      {
        scrollOffset = selectedIndex;
      }
      else if (selectedIndex >= scrollOffset + visibleItems)
      {
        scrollOffset = selectedIndex - visibleItems + 1;
      }

      // Показываем видимые пункты
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

      // Показываем информацию о прокрутке
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
            // Если в начале списка, переходим в конец
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
            // Если в конце списка, переходим в начало
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
            // Выбран пункт "Ввести новый IP-адрес"
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

      // Проверяем подключение
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
                           "Выполнить команду (CMD)",
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
            await HandleCmdCommand();
          }
          else if (selectedIndex == 3)
          {
            await ChangeServer();
          }
          else if (selectedIndex == 4)
          {
            Console.WriteLine("До свидания!");
            return;
          }
          break;
      }
    }
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

      // Корректируем scrollOffset чтобы выбранный элемент был в видимой области
      if (selectedIndex < scrollOffset)
      {
        scrollOffset = selectedIndex;
      }
      else if (selectedIndex >= scrollOffset + visibleItems)
      {
        scrollOffset = selectedIndex - visibleItems + 1;
      }

      // Показываем видимые пункты
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

      // Показываем информацию о прокрутке
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

  static async Task HandleCmdCommand()
  {
    while (true)
    {
      Console.Clear();

      int windowWidth = Console.WindowWidth - 1;
      string topLine = "+" + new string('-', windowWidth - 2) + "+";
      string title = "ВЫПОЛНЕНИЕ КОМАНДЫ";
      int padding = (windowWidth - 2 - title.Length) / 2;
      if (padding < 0) padding = 0;
      string titleLine = "|" + new string(' ', padding) + title + new string(' ', windowWidth - 2 - padding - title.Length) + "|";

      Console.WriteLine(topLine);
      Console.WriteLine(titleLine);
      Console.WriteLine(topLine);
      Console.WriteLine();
      Console.WriteLine("  (Esc для возврата в главное меню)");
      Console.WriteLine();
      Console.Write("  Введите команду (например: df -h, ps aux, ls -la): ");

      string command = await ReadLineWithEscapeAsync();
      if (command == null) return;

      if (string.IsNullOrEmpty(command))
      {
        Console.WriteLine("  Команда не может быть пустой");
        Console.WriteLine("\n  Нажмите Esc для возврата в меню или любую клавишу для повтора...");
        var key = Console.ReadKey(true);
        if (key.Key == ConsoleKey.Escape)
          return;
        continue;
      }

      try
      {
        using (TcpClient client = new TcpClient())
        {
          Console.WriteLine($"\n  Подключение к серверу {ServerIp}:{ServerPort}...");
          await client.ConnectAsync(ServerIp, ServerPort);
          Console.WriteLine("  Подключение установлено");

          using (NetworkStream stream = client.GetStream())
          {
            string request = $"CMD {command}\n";
            byte[] requestBytes = Encoding.UTF8.GetBytes(request);
            await stream.WriteAsync(requestBytes, 0, requestBytes.Length);
            Console.WriteLine($"  Команда отправлена: {command}");

            await ReceiveData(stream, true, true);
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
      Console.WriteLine();
      Console.Write("  Введите путь к файлу: ");

      string filepath = await ReadLineWithEscapeAsync();
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
        Console.WriteLine("  +-----------------------------------------------------+");

        AskToShowFile(lastDownloadedFile);
      }

      Console.WriteLine("\n  Нажмите Esc для возврата в меню или любую клавишу для повтора...");
      var exitKey = Console.ReadKey(true);
      if (exitKey.Key == ConsoleKey.Escape)
        return;
    }
  }

  static void AskToShowFile(string filePath)
  {
    Console.WriteLine("\n  Показать содержимое скачанного файла?");
    Console.Write("  [Y] Да  [N] Нет: ");

    while (true)
    {
      var key = Console.ReadKey(true);
      if (key.Key == ConsoleKey.Y)
      {
        Console.WriteLine("Y");
        ShowFileContent(filePath);
        break;
      }
      else if (key.Key == ConsoleKey.N)
      {
        Console.WriteLine("N");
        Console.WriteLine("  Просмотр отменен");
        break;
      }
      else if (key.Key == ConsoleKey.Escape)
      {
        Console.WriteLine("N");
        Console.WriteLine("  Просмотр отменен");
        break;
      }
    }
  }

  static void ShowFileContent(string filePath)
  {
    try
    {
      Console.Clear();

      int windowWidth = Console.WindowWidth - 1;
      string topLine = "+" + new string('-', windowWidth - 2) + "+";
      string title = $"СОДЕРЖИМОЕ ФАЙЛА: {Path.GetFileName(filePath)}";
      int padding = (windowWidth - 2 - title.Length) / 2;
      if (padding < 0) padding = 0;
      string titleLine = "|" + new string(' ', padding) + title + new string(' ', windowWidth - 2 - padding - title.Length) + "|";

      Console.WriteLine(topLine);
      Console.WriteLine(titleLine);
      Console.WriteLine(topLine);
      Console.WriteLine();

      string extension = Path.GetExtension(filePath).ToLower();

      if (extension == ".txt" || extension == ".log" || extension == ".cfg" ||
          extension == ".conf" || extension == ".ini" || extension == ".xml" ||
          extension == ".json" || extension == ".cs" || extension == ".cpp" ||
          extension == ".h" || extension == ".py" || extension == ".js" ||
          extension == ".html" || extension == ".css" || extension == ".md" ||
          extension == ".bat" || extension == ".sh" || extension == ".ps1")
      {
        string content = File.ReadAllText(filePath, Encoding.UTF8);

        if (string.IsNullOrWhiteSpace(content))
        {
          Console.WriteLine("  [Файл пуст]");
        }
        else
        {
          string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
          int lineNumber = 1;
          int maxLines = Console.WindowHeight - 10;

          if (lines.Length > maxLines)
          {
            Console.WriteLine($"  [Файл слишком большой, показываются первые {maxLines} строк из {lines.Length}]\n");

            for (int i = 0; i < maxLines && i < lines.Length; i++)
            {
              Console.ForegroundColor = ConsoleColor.DarkGray;
              Console.Write($"  {lineNumber,4} ");
              Console.ResetColor();

              string line = lines[i];
              if (line.Length > Console.WindowWidth - 10)
              {
                line = line.Substring(0, Console.WindowWidth - 15) + "...";
              }
              Console.WriteLine(line);
              lineNumber++;
            }

            Console.WriteLine($"\n  ... и еще {lines.Length - maxLines} строк(и)");
          }
          else
          {
            foreach (string line in lines)
            {
              Console.ForegroundColor = ConsoleColor.DarkGray;
              Console.Write($"  {lineNumber,4} ");
              Console.ResetColor();

              string displayLine = line;
              if (displayLine.Length > Console.WindowWidth - 10)
              {
                displayLine = displayLine.Substring(0, Console.WindowWidth - 15) + "...";
              }
              Console.WriteLine(displayLine);
              lineNumber++;
            }
          }
        }
      }
      else
      {
        FileInfo info = new FileInfo(filePath);
        Console.WriteLine($"  [Бинарный файл - невозможно отобразить в текстовом виде]");
        Console.WriteLine($"  Имя: {Path.GetFileName(filePath)}");
        Console.WriteLine($"  Размер: {FormatBytes(info.Length)}");
        Console.WriteLine($"  Тип: {extension.ToUpperInvariant()}");
      }

      Console.WriteLine($"\n  Путь: {filePath}");
      Console.WriteLine("\n  Нажмите любую клавишу для продолжения...");
      Console.ReadKey(true);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"\n  Ошибка при чтении файла: {ex.Message}");
      Console.WriteLine("\n  Нажмите любую клавишу...");
      Console.ReadKey(true);
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
    string[] lines = textOutput.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

    List<DirItem> directories = new List<DirItem>();
    List<DirItem> files = new List<DirItem>();

    foreach (string line in lines)
    {
      if (string.IsNullOrWhiteSpace(line))
        continue;

      string[] parts = line.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
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
}