using System;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

class TcpFileClient
{
  private const string ServerIp = "172.22.129.222";
  private const int ServerPort = 55555;
  private const int PacketSize = 512;
  private static string lastDownloadedFile = "";
  private static string lastRequestedFile = "";

  static async Task Main(string[] args)
  {
    Console.WriteLine("========================================");
    Console.WriteLine("TCP файловый клиент");
    Console.WriteLine("========================================");

    while (true)
    {
      Console.WriteLine("\nМеню:");
      Console.WriteLine("1. Показать список файлов в директории (DIR)");
      Console.WriteLine("2. Скачать файл (FILE)");
      Console.WriteLine("3. Выполнить команду (CMD)");
      Console.WriteLine("4. Выход");
      Console.Write("\nВыберите действие (1-4): ");

      string choice = Console.ReadLine();

      if (choice == "1")
      {
        await HandleDirCommand();
      }
      else if (choice == "2")
      {
        await HandleFileDownload();
      }
      else if (choice == "3")
      {
        await HandleCmdCommand();
      }
      else if (choice == "4")
      {
        Console.WriteLine("До свидания!");
        break;
      }
      else
      {
        Console.WriteLine("Неверный выбор. Пожалуйста, выберите 1, 2, 3 или 4.");
      }
    }
  }

  static async Task HandleCmdCommand()
  {
    Console.Write("Введите команду (например: df -h, ps aux, ls -la): ");
    string command = Console.ReadLine();

    if (string.IsNullOrEmpty(command))
    {
      Console.WriteLine("Команда не может быть пустой");
      return;
    }

    try
    {
      using (TcpClient client = new TcpClient())
      {
        Console.WriteLine($"Подключение к серверу {ServerIp}:{ServerPort}...");
        await client.ConnectAsync(ServerIp, ServerPort);
        Console.WriteLine("Подключение установлено");

        using (NetworkStream stream = client.GetStream())
        {
          string request = $"CMD {command}\n";
          byte[] requestBytes = Encoding.UTF8.GetBytes(request);
          await stream.WriteAsync(requestBytes, 0, requestBytes.Length);
          Console.WriteLine($"Команда отправлена: {command}");

          await ReceiveData(stream, true, true); // Текстовый вывод, это команда
        }
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"\nОшибка: {ex.Message}");
    }

    Console.WriteLine("\nНажмите любую клавишу для продолжения...");
    Console.ReadKey();
  }

  static async Task HandleDirCommand()
  {
    Console.Write("Введите путь к директории (или нажмите Enter для текущей): ");
    string dirpath = Console.ReadLine();

    try
    {
      using (TcpClient client = new TcpClient())
      {
        Console.WriteLine($"Подключение к серверу {ServerIp}:{ServerPort}...");
        await client.ConnectAsync(ServerIp, ServerPort);
        Console.WriteLine("Подключение установлено");

        using (NetworkStream stream = client.GetStream())
        {
          string request = string.IsNullOrEmpty(dirpath) ? "DIR\n" : $"DIR {dirpath}\n";
          byte[] requestBytes = Encoding.UTF8.GetBytes(request);
          await stream.WriteAsync(requestBytes, 0, requestBytes.Length);
          Console.WriteLine($"Запрос DIR отправлен: {(string.IsNullOrEmpty(dirpath) ? "текущая директория" : dirpath)}");

          await ReceiveData(stream, true, false); // Текстовый вывод, это DIR
        }
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"\nОшибка: {ex.Message}");
    }

    Console.WriteLine("\nНажмите любую клавишу для продолжения...");
    Console.ReadKey();
  }

  static async Task HandleFileDownload()
  {
    Console.Write("Введите путь к файлу: ");
    string filepath = Console.ReadLine();

    if (string.IsNullOrEmpty(filepath))
    {
      Console.WriteLine("Путь к файлу не может быть пустым");
      return;
    }

    lastRequestedFile = filepath;

    try
    {
      using (TcpClient client = new TcpClient())
      {
        Console.WriteLine($"Подключение к серверу {ServerIp}:{ServerPort}...");
        await client.ConnectAsync(ServerIp, ServerPort);
        Console.WriteLine("Подключение установлено");

        using (NetworkStream stream = client.GetStream())
        {
          string request = $"{filepath}\n";
          byte[] requestBytes = Encoding.UTF8.GetBytes(request);
          await stream.WriteAsync(requestBytes, 0, requestBytes.Length);
          Console.WriteLine($"Запрос FILE отправлен: {filepath}");

          await ReceiveData(stream, false, false); // Бинарные данные, это файл
        }
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"\nОшибка: {ex.Message}");
    }

    Console.WriteLine("\nНажмите любую клавишу для продолжения...");
    Console.ReadKey();
  }

  static async Task ReceiveData(NetworkStream stream, bool isTextOutput, bool isCommand)
  {
    byte[] buffer = new byte[1024];

    int bytesRead = await ReadLineAsync(stream, buffer);
    if (bytesRead <= 0)
    {
      Console.WriteLine("Ошибка: соединение разорвано");
      return;
    }

    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

    if (response.StartsWith("ERROR"))
    {
      Console.WriteLine($"Ошибка от сервера: {response}");
      return;
    }

    if (!response.StartsWith("START"))
    {
      Console.WriteLine($"Неожиданный ответ: {response}");
      return;
    }

    string[] parts = response.Split(' ');
    if (parts.Length != 2 || !int.TryParse(parts[1], out int totalPackets))
    {
      Console.WriteLine($"Неверный формат START: {response}");
      return;
    }

    Console.WriteLine($"Получен START: всего пакетов {totalPackets}");

    string confirm = $"OK {totalPackets}\n";
    byte[] confirmBytes = Encoding.UTF8.GetBytes(confirm);
    await stream.WriteAsync(confirmBytes, 0, confirmBytes.Length);
    Console.WriteLine("Отправлено подтверждение START");

    using (MemoryStream dataStream = new MemoryStream())
    {
      int receivedPackets = 0;
      bool success = true;

      Console.WriteLine("\nНачало получения данных...");
      Console.WriteLine("----------------------------------------");

      while (receivedPackets < totalPackets)
      {
        bytesRead = await ReadLineAsync(stream, buffer);
        if (bytesRead <= 0)
        {
          Console.WriteLine("Ошибка: соединение разорвано");
          success = false;
          break;
        }

        string packetHeader = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

        if (packetHeader == "END")
        {
          Console.WriteLine("Получен END");
          break;
        }

        parts = packetHeader.Split(' ');
        if (parts.Length != 3 || parts[0] != "PACKET" ||
            !int.TryParse(parts[1], out int packetNum) ||
            !int.TryParse(parts[2], out int packetSize))
        {
          Console.WriteLine($"Неверный формат пакета: {packetHeader}");
          success = false;
          break;
        }

        if (packetNum != receivedPackets + 1)
        {
          Console.WriteLine($"Ошибка: ожидался пакет {receivedPackets + 1}, получен {packetNum}");
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
            Console.WriteLine("Ошибка чтения данных пакета");
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

        int percent = (receivedPackets + 1) * 100 / totalPackets;
        Console.Write($"\rПолучено: {receivedPackets + 1}/{totalPackets} пакетов ({percent}%)");
        receivedPackets++;
      }

      Console.WriteLine();
      Console.WriteLine("----------------------------------------");

      if (success && receivedPackets == totalPackets)
      {
        bytesRead = await ReadLineAsync(stream, buffer);
        if (bytesRead > 0)
        {
          string endMsg = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
          if (endMsg == "END")
          {
            Console.WriteLine("Передача успешно завершена");

            dataStream.Seek(0, SeekOrigin.Begin);
            byte[] receivedData = dataStream.ToArray();

            if (isTextOutput)
            {
              // Текстовый вывод (DIR или CMD)
              string textOutput = Encoding.UTF8.GetString(receivedData);

              if (isCommand)
              {
                // Для команд CMD выводим без форматирования
                Console.WriteLine("\n=== РЕЗУЛЬТАТ КОМАНДЫ ===");
                Console.WriteLine(textOutput);
              }
              else
              {
                // Для DIR выводим с форматированием
                Console.WriteLine("\n=== СОДЕРЖИМОЕ ДИРЕКТОРИИ ===");
                FormatDirectoryListing(textOutput);
              }
            }
            else
            {
              // Бинарные данные (файл) - сохраняем на диск
              try
              {
                dataStream.Seek(0, SeekOrigin.Begin);
                string outputFileName = GenerateFileNameFromPath(lastRequestedFile);

                using (var gzipStream = new GZipStream(dataStream, CompressionMode.Decompress))
                using (var outputStream = File.Create(outputFileName))
                {
                  await gzipStream.CopyToAsync(outputStream);
                  Console.WriteLine($"\nФайл успешно скачан и распакован: {outputFileName}");
                  lastDownloadedFile = outputFileName;
                  FileInfo fileInfo = new FileInfo(outputFileName);
                  Console.WriteLine($"Размер файла: {fileInfo.Length:N0} байт");
                }
              }
              catch (Exception ex)
              {
                Console.WriteLine($"\nНе удалось распаковать как gzip: {ex.Message}");
                string outputFileName = GenerateFileNameFromPath(lastRequestedFile) + ".bin";
                File.WriteAllBytes(outputFileName, receivedData);
                Console.WriteLine($"Данные сохранены как: {outputFileName}");
                Console.WriteLine($"Размер: {receivedData.Length:N0} байт");
                lastDownloadedFile = outputFileName;
              }
            }
          }
        }
      }
      else
      {
        Console.WriteLine($"Ошибка: получено {receivedPackets} из {totalPackets} пакетов");
      }
    }
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

      // Парсим строку формата: "размер имя"
      string[] parts = line.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length >= 2)
      {
        string sizeStr = parts[0];
        string name = string.Join(" ", parts, 1, parts.Length - 1);

        DirItem item = new DirItem();

        // Конвертируем размер
        if (double.TryParse(sizeStr, out double sizeInKB))
        {
          item.Size = sizeInKB;
        }
        else
        {
          item.Size = 0;
        }

        // Определяем тип элемента по суффиксу
        if (name.EndsWith("/"))
        {
          item.Name = name.TrimEnd('/');
          item.Type = ItemType.Directory;
          directories.Add(item);
        }
        else
        {
          item.Name = name;

          // Определяем тип файла по суффиксу
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
          else if (name.EndsWith(".gz") || name.EndsWith(".tgz"))
          {
            item.Type = ItemType.Archive;
          }
          else
          {
            item.Type = ItemType.File;
          }

          files.Add(item);
        }
      }
    }

    // Сортируем по алфавиту
    directories.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    files.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

    // Выводим директории
    if (directories.Count > 0)
    {
      PrintItemsVertically(directories, true);
    }

    // Выводим файлы
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

    // Определяем заголовок в зависимости от типа
    if (items[0].Type == ItemType.Directory)
    {
      Console.ForegroundColor = ConsoleColor.Cyan;
      Console.WriteLine("=== ДИРЕКТОРИИ ===");
      Console.ResetColor();
    }
    else
    {
      Console.WriteLine("=== ФАЙЛЫ ===");
    }

    int columns = 4;
    int rows = (int)Math.Ceiling((double)items.Count / columns);

    // Вычисляем максимальную ширину имени и максимальную ширину размера для каждой колонки
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

    // Выводим элементы вертикально
    for (int row = 0; row < rows; row++)
    {
      for (int col = 0; col < columns; col++)
      {
        int index = col * rows + row;
        if (index < items.Count)
        {
          var item = items[index];

          // Устанавливаем цвет в зависимости от типа
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
            else if (item.Type == ItemType.Archive)
            {
              Console.ForegroundColor = ConsoleColor.Magenta;
            }
            else
            {
              Console.ForegroundColor = ConsoleColor.White;
            }
          }

          string sizeStr = $"{item.Size:F1} kb";
          // Имя выравниваем по левому краю, размер - по правому краю в конце колонки
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
          // Пустое место для отсутствующих элементов
          if (col < columns - 1)
          {
            Console.Write(new string(' ', columnNameWidths[col] + columnSizeWidths[col] + 3));
          }
        }
      }
      Console.WriteLine();
    }
  }

  // Класс для хранения информации о директории/файле
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
    Symlink,
    Archive
  }
}