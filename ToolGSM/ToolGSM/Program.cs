using System;
using System.IO.Ports;
using System.Threading;
using System.Text.RegularExpressions;
using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== CÔNG CỤ QUÉT GSM VÀ ĐỌC SMS ===");
        Console.WriteLine("Quét COM3 → COM10 và đọc tin nhắn chưa đọc");
        Console.WriteLine("Nhấn phím bất kỳ để bắt đầu...");
        Console.ReadKey();
        Console.Clear();
        
        ScanGSMPortsAndReadSMS();
        
        Console.WriteLine("\nNhấn phím bất kỳ để thoát...");
        Console.ReadKey();
    }

    static void ScanGSMPortsAndReadSMS()
    {
        Console.WriteLine("🔍 Bắt đầu quét COM port từ COM3 đến COM10...\n");
        
        // Quét từ COM3 đến COM10
        for (int i = 0; i <= 10; i++)
        {
            string portName = $"COM{i}";
            Console.WriteLine($"📡 Đang kiểm tra {portName}...");
            
            try
            {
                if (TestGSMPort(portName))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ {portName} là thiết bị GSM hợp lệ!");
                    Console.ResetColor();
                    
                    // Đọc SMS từ port này
                    ReadSMSFromPort(portName);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"❌ {portName} không phải thiết bị GSM");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"⚠️ Lỗi khi truy cập {portName}: {ex.Message}");
                Console.ResetColor();
            }
            
            Console.WriteLine();
        }
    }

    static bool TestGSMPort(string portName)
    {
        try
        {
            using (SerialPort serialPort = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One))
            {
                serialPort.ReadTimeout = 3000;
                serialPort.WriteTimeout = 3000;
                serialPort.Open();
                
                Thread.Sleep(500);
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();
                
                // Gửi AT command
                serialPort.Write("AT\r\n");
                Thread.Sleep(1000);
                
                string response = serialPort.ReadExisting();
                return response.Contains("OK");
            }
        }
        catch
        {
            return false;
        }
    }

    static void ReadSMSFromPort(string portName)
    {
        Console.WriteLine($"📧 Đang đọc SMS từ {portName}...");
        
        try
        {
            using (SerialPort serialPort = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One))
            {
                serialPort.ReadTimeout = 5000;
                serialPort.WriteTimeout = 5000;
                serialPort.Open();
                
                Thread.Sleep(500);
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();
                
                // Bước 1: Thiết lập chế độ text mode
                Console.WriteLine("   📝 Thiết lập chế độ text mode (AT+CMGF=1)...");
                serialPort.Write("AT+CMGF=1\r\n");
                Thread.Sleep(1000);
                string response1 = serialPort.ReadExisting();
                
                if (!response1.Contains("OK"))
                {
                    Console.WriteLine("   ❌ Không thể thiết lập text mode");
                    return;
                }
                
                Console.WriteLine("   ✅ Text mode OK");
                
                // Bước 2: Đọc tin nhắn chưa đọc
                Console.WriteLine("   📬 Đọc tin nhắn chưa đọc (AT+CMGL=\"REC UNREAD\")...");
                serialPort.DiscardInBuffer();
                serialPort.Write("AT+CMGL=\"REC UNREAD\"\r\n");
                Thread.Sleep(2000);
                
                string smsResponse = serialPort.ReadExisting();
                
                if (string.IsNullOrEmpty(smsResponse) || smsResponse.Contains("ERROR"))
                {
                    Console.WriteLine("   ❌ Không thể đọc SMS hoặc có lỗi");
                    return;
                }
                
                // Parse và hiển thị SMS
                ParseAndDisplaySMS(smsResponse);
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"   ⚠️ Lỗi khi đọc SMS: {ex.Message}");
            Console.ResetColor();
        }
    }

    static void ParseAndDisplaySMS(string smsData)
    {
        Console.WriteLine("   🔍 Đang phân tích dữ liệu SMS...");
        
        if (smsData.Contains("OK") && !smsData.Contains("+CMGL:"))
        {
            Console.WriteLine("   📭 Không có tin nhắn chưa đọc");
            return;
        }
        
        // Pattern để parse SMS
        // Format: +CMGL: index,"REC UNREAD","sender",,"date,time"
        string pattern = @"\+CMGL:\s*(\d+),""([^""]*)"",""([^""]*)"",""([^""]*)"",""([^""]*)""";
        MatchCollection matches = Regex.Matches(smsData, pattern);
        
        if (matches.Count == 0)
        {
            Console.WriteLine("   📭 Không tìm thấy tin nhắn hợp lệ");
            Console.WriteLine($"   Raw data: {smsData}");
            return;
        }
        
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"   📨 Tìm thấy {matches.Count} tin nhắn chưa đọc:");
        Console.ResetColor();
        
        string[] lines = smsData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        
        for (int i = 0; i < matches.Count; i++)
        {
            Match match = matches[i];
            
            string index = match.Groups[1].Value;
            string status = match.Groups[2].Value;
            string sender = match.Groups[3].Value;
            string name = match.Groups[4].Value;
            string datetime = match.Groups[5].Value;
            
            // Tìm nội dung tin nhắn (dòng sau header)
            string content = "";
            for (int j = 0; j < lines.Length; j++)
            {
                if (lines[j].Contains($"+CMGL: {index}"))
                {
                    if (j + 1 < lines.Length)
                    {
                        content = lines[j + 1].Trim();
                    }
                    break;
                }
            }
            
            // Hiển thị thông tin SMS
            Console.WriteLine($"   ───────────────────────────────");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"   📩 SMS #{index}");
            Console.ResetColor();
            Console.WriteLine($"   👤 Từ: {sender}");
            Console.WriteLine($"   🏷️ Tên: {name}");
            Console.WriteLine($"   📅 Thời gian: {datetime}");
            Console.WriteLine($"   💬 Nội dung: {content}");
            Console.WriteLine($"   📊 Trạng thái: {status}");
        }
        
        Console.WriteLine($"   ───────────────────────────────");
    }
}
