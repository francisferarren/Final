using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
namespace DailyReflectionJournal
{
   static class Utils
   {
       public static void Pause(string message = "Press any key to continue...")
       {
           Console.WriteLine($"\n{message}");
           Console.ReadKey(true);
           Console.Clear();
       }
       public static string Normalize(string text) =>
           string.IsNullOrWhiteSpace(text) ? "unknown" : text.Trim().ToLowerInvariant();
   }
   class JournalEntry
   {
       public string Date { get; set; } = string.Empty;
       public string Mood { get; set; } = string.Empty;
       public string Reflection { get; set; } = string.Empty;
       public string EmotionNotes { get; set; } = string.Empty;
       // One-line storage format (matching your requested format)
       public virtual string ToFileFormat() =>
           $"{Date}|{Mood}|{Reflection}|{EmotionNotes}";
   }
   class MoodEntry : JournalEntry
   {
       public string EmotionCategory { get; set; } = string.Empty;
       public int MoodRating { get; set; } = 5;
       // AssignRating remains — intensity is derived automatically from Mood
       public void AssignRating()
       {
           string mood = Utils.Normalize(Mood);
           switch (mood)
           {
               case "happy":
               case "joyful":
                   MoodRating = 9; EmotionCategory = "Excellent"; break;
               case "calm":
               case "relaxed":
                   MoodRating = 8; EmotionCategory = "Good"; break;
               case "neutral":
               case "fine":
                   MoodRating = 5; EmotionCategory = "Moderate"; break;
               case "sad":
               case "down":
                   MoodRating = 3; EmotionCategory = "Low"; break;
               case "angry":
               case "annoyed":
                   MoodRating = 2; EmotionCategory = "Low"; break;
               default:
                   MoodRating = 5; EmotionCategory = "Unknown"; break;
           }
       }
       // Keep one-line file format including rating & category so we can reload easily
       public override string ToFileFormat() =>
           $"{Date}|{Mood}|{MoodRating}|{EmotionCategory}|{Reflection}|{EmotionNotes}";
   }
   class JournalManager
   {
       private readonly List<MoodEntry> entries = new();
       private readonly string filePath;
       private readonly string reportPath;
       public JournalManager(string username)
       {
           string safeUser = string.IsNullOrWhiteSpace(username) ? "default" : username;
           // Journal file name as requested: Journal_<Name>_Entries.txt
           filePath = $"Journal_{safeUser}_Entries.txt";
           // Report file name: ShowMoodReport_<Name>.txt
           reportPath = $"ShowMoodReport_{safeUser}.txt";
           LoadEntries();
       }
       private void LoadEntries()
       {
           if (!File.Exists(filePath)) return;
           foreach (var raw in File.ReadAllLines(filePath))
           {
               if (string.IsNullOrWhiteSpace(raw)) continue;
               // Split into parts, don't remove empty entries (but trim spaces)
               var parts = raw.Split('|');
               // Expected: at least 6 parts: date, mood, rating, category, reflection, emotionnotes
               if (parts.Length < 6) continue;
               string dateStr = parts[0].Trim();
               string mood = parts[1].Trim();
               int rating = int.TryParse(parts[2].Trim(), out int r) ? r : 5;
               string category = parts[3].Trim();
               string reflection = parts[4].Trim();
               // Join any remaining parts into emotionNotes to handle extra '|' inside notes
               string emotionNotes = string.Join("|", parts.Skip(5)).Trim();
               // Store in list
               entries.Add(new MoodEntry
               {
                   Date = dateStr,
                   Mood = mood,
                   MoodRating = rating,
                   EmotionCategory = category,
                   Reflection = reflection,
                   EmotionNotes = emotionNotes
               });
           }
       }
       private void SaveAll() =>
           File.WriteAllLines(filePath, entries.Select(e => e.ToFileFormat()));
       public void AddEntry()
       {
           Console.Clear();
           var entry = new MoodEntry();
           entry.Date = DateTime.Now.ToString("MM/dd/yyyy hh:mm tt");
           Console.WriteLine($"Date: {entry.Date}");
           Console.Write("Mood (Happy, Sad, Calm, Angry, Neutral): ");
           entry.Mood = Console.ReadLine() ?? "unknown";
           entry.AssignRating(); // automatically sets MoodRating & EmotionCategory
           Console.Write("Tell me about your day: ");
           entry.Reflection = Console.ReadLine() ?? "";
           Console.Write("What event caused you to feel such emotion? ");
           string q1 = Console.ReadLine() ?? "";
           Console.Write("How did you respond to the situation or emotion you experienced? ");
           string q2 = Console.ReadLine() ?? "";
           // Use consistent one-line format: "EmotionalResponse: ... | Response: ..."
           entry.EmotionNotes = $"EmotionalResponse: {q1} | Response: {q2}";
           entries.Add(entry);
           File.AppendAllText(filePath, entry.ToFileFormat() + Environment.NewLine);
           Console.Clear();
           Console.WriteLine("\n✅ Entry added successfully!\n");
           Utils.Pause();
       }
       public void ViewEntries()
       {
           Console.Clear();
           if (entries.Count == 0)
           {
               Console.WriteLine("No entries found.");
               Utils.Pause();
               return;
           }
           Console.WriteLine("Date & Time           | Mood     | Rating | Category    | Reflection");
           Console.WriteLine("--------------------------------------------------------------------------");
           // Order by safe parsed date (avoids exceptions for unexpected formats)
           foreach (var e in entries.OrderByDescending(x => ParseDateSafe(x.Date)))
           {
               // Main entry line
               Console.WriteLine($"{e.Date,-20} | {e.Mood,-8} | {e.MoodRating,6} | {e.EmotionCategory,-11} | {e.Reflection}");
               // Normalize the stored EmotionNotes so we can reliably parse EmotionalResponse and Response.
               // Accept common variations and trim.
               string notes = (e.EmotionNotes ?? "").Trim();
               // Normalize some variants (optional)
               notes = notes.Replace("Emotional Response:", "EmotionalResponse:", StringComparison.OrdinalIgnoreCase)
                            .Replace("emotional response:", "EmotionalResponse:", StringComparison.OrdinalIgnoreCase)
                            .Replace("|Response:", "|Response:", StringComparison.OrdinalIgnoreCase)
                            .Replace("| Response:", "|Response:", StringComparison.OrdinalIgnoreCase)
                            .Trim();
               string emotionalResponse = "";
               string response = "";
               // Try to find the delimiter "|Response:" (case-insensitive)
               int idx = IndexOfIgnoreCase(notes, "|Response:");
               if (idx >= 0)
               {
                   emotionalResponse = notes.Substring(0, idx).Trim();
                   response = notes.Substring(idx + "|Response:".Length).Trim();
               }
               else
               {
                   // If no explicit Response marker, try to split on the first " | " if present
                   int pipeIdx = notes.IndexOf('|');
                   if (pipeIdx >= 0)
                   {
                       emotionalResponse = notes.Substring(0, pipeIdx).Trim();
                       response = notes.Substring(pipeIdx + 1).Trim();
                   }
                   else
                   {
                       // Entire notes considered as EmotionalResponse if no separator found
                       emotionalResponse = notes;
                       response = "";
                   }
               }
               // Remove label prefixes if still present
               emotionalResponse = RemovePrefixIgnoreCase(emotionalResponse, "EmotionalResponse:");
               emotionalResponse = emotionalResponse.Trim();
               response = RemovePrefixIgnoreCase(response, "Response:");
               response = response.Trim();
               // Finally print both pieces on a single line (guaranteeing Response is shown, even if empty)
               Console.WriteLine($"    EmotionalResponse: {emotionalResponse} | Response: {response}");
               Console.WriteLine();
           }
           Utils.Pause();
       }
       // small helpers for case-insensitive operations
       private static int IndexOfIgnoreCase(string source, string value)
       {
           return CultureInfo.InvariantCulture.CompareInfo.IndexOf(source ?? "", value ?? "", CompareOptions.IgnoreCase);
       }
       private static string RemovePrefixIgnoreCase(string source, string prefix)
       {
           if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(prefix)) return source ?? "";
           if (source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
               return source.Substring(prefix.Length);
           return source;
       }
       // Edit entry with error handling
       public void EditEntry()
       {
           Console.Clear();
           if (entries.Count == 0)
           {
               Console.WriteLine("No entries available to edit.");
               Utils.Pause();
               return;
           }
           Console.WriteLine("Select an entry to edit:\n");
           for (int i = 0; i < entries.Count; i++)
           {
               Console.WriteLine($"{i + 1}. {entries[i].Date} - {entries[i].Mood}");
           }
           Console.Write("\nEnter entry number: ");
           if (!int.TryParse(Console.ReadLine(), out int choice) || choice < 1 || choice > entries.Count)
           {
               Console.WriteLine("Invalid selection.");
               Utils.Pause();
               return;
           }
           var entry = entries[choice - 1];
           Console.Clear();
           Console.WriteLine($"Editing Entry — {entry.Date}\n");
           Console.Write($"New Mood (leave blank to keep '{entry.Mood}'): ");
           string nm = Console.ReadLine() ?? "";
           if (!string.IsNullOrWhiteSpace(nm))
           {
               entry.Mood = nm;
               entry.AssignRating(); // recalculate category & rating from new mood
           }
           Console.Write($"New Reflection (leave blank to keep old): ");
           string newRef = Console.ReadLine()!;
           if (!string.IsNullOrWhiteSpace(newRef)) entry.Reflection = newRef;
           Console.Write($"New EmotionNotes (leave blank to keep old): ");
           string newNotes = Console.ReadLine()!;
           if (!string.IsNullOrWhiteSpace(newNotes)) entry.EmotionNotes = newNotes;
           SaveAll();
           Console.Clear();
           Console.WriteLine("✏️ Entry updated successfully!");
           Utils.Pause();
       }
       // Delete entry with error handling and confirmation
       public void DeleteEntry()
       {
           Console.Clear();
           if (entries.Count == 0)
           {
               Console.WriteLine("No entries available to delete.");
               Utils.Pause();
               return;
           }
           Console.WriteLine("Select an entry to delete:\n");
           for (int i = 0; i < entries.Count; i++)
           {
               Console.WriteLine($"{i + 1}. {entries[i].Date} - {entries[i].Mood}");
           }
           Console.Write("\nEnter entry number: ");
           if (!int.TryParse(Console.ReadLine(), out int choice) || choice < 1 || choice > entries.Count)
           {
               Console.WriteLine("Invalid selection.");
               Utils.Pause();
               return;
           }
           var entry = entries[choice - 1];
           Console.Write($"\nAre you sure you want to delete entry from {entry.Date}? (Y/N): ");
           string confirm = (Console.ReadLine() ?? "").Trim().ToLower();
           if (confirm == "y" || confirm == "yes")
           {
               entries.RemoveAt(choice - 1);
               SaveAll();
               Console.Clear();
               Console.WriteLine("🗑️ Entry deleted successfully!");
           }
           else
           {
               Console.WriteLine("Deletion canceled.");
           }
           Utils.Pause();
       }
       public void ShowMoodReport()
       {
           Console.Clear();
           if (entries.Count == 0)
           {
               Console.WriteLine("No entries to analyze.");
               Utils.Pause();
               return;
           }
           DateTime now = DateTime.Now;
           // Safe date parse method (accepts your standard format, fallback to DateTime.TryParse)
           DateTime ParseDateSafe(string dateText)
           {
               if (string.IsNullOrWhiteSpace(dateText)) return DateTime.MinValue;
               string[] formats =
               {
                   "MM/dd/yyyy hh:mm tt",
                   "M/d/yyyy hh:mm tt",
                   "MM/dd/yyyy h:mm tt",
                   "M/d/yyyy h:mm tt",
                   "MM/dd/yyyy",
                   "M/d/yyyy"
               };
               if (DateTime.TryParseExact(dateText, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                   return parsed;
               if (DateTime.TryParse(dateText, out parsed))
                   return parsed;
               return DateTime.MinValue;
           }
           var weeklyEntries = entries.Where(e => ParseDateSafe(e.Date) >= now.AddDays(-7)).ToList();
           var monthlyEntries = entries.Where(e => ParseDateSafe(e.Date) >= now.AddDays(-30)).ToList();
           Console.WriteLine("\n========== MOOD REPORT ==========\n");
           Console.WriteLine("Generated: " + now.ToString("MM/dd/yyyy hh:mm tt"));
           Console.WriteLine("---------------------------------------------------\n");
           // WEEKLY
           Console.WriteLine("************ WEEKLY MOOD REPORT (Last 7 Days) ************\n");
           DisplayMoodTable(weeklyEntries);
           // MONTHLY
           Console.WriteLine("************ MONTHLY MOOD REPORT (Last 30 Days) ************\n");
           DisplayMoodTable(monthlyEntries);
           // OVERALL
           Console.WriteLine("************ OVERALL MOOD SUMMARY ************\n");
           DisplayMoodTable(entries);
           // Save text report (same content in file)
           try
           {
               using (var sw = new StreamWriter(reportPath, false))
               {
                   sw.WriteLine($"Mood Report for {Path.GetFileNameWithoutExtension(filePath)}");
                   sw.WriteLine($"Generated: {now:MM/dd/yyyy hh:mm tt}\n");
                   sw.WriteLine("WEEKLY MOOD REPORT (Last 7 Days)\n");
                   WriteMoodTableToStream(sw, weeklyEntries);
                   sw.WriteLine("\nMONTHLY MOOD REPORT (Last 30 Days)\n");
                   WriteMoodTableToStream(sw, monthlyEntries);
                   sw.WriteLine("\nOVERALL MOOD SUMMARY\n");
                   WriteMoodTableToStream(sw, entries);
               }
               Console.WriteLine($"\n✅ Report saved to {reportPath}");
           }
           catch (Exception ex)
           {
               Console.WriteLine($"⚠️ Could not save report: {ex.Message}");
           }
           Utils.Pause();
       }
       // Helper: write the same grouped table into a StreamWriter
       private void WriteMoodTableToStream(StreamWriter sw, List<MoodEntry> list)
       {
           if (list == null || list.Count == 0)
           {
               sw.WriteLine("No data available.\n");
               return;
           }
           var grouped = list
               .GroupBy(e => string.IsNullOrWhiteSpace(e.Mood) ? "Unknown" : e.Mood)
               .Select(g => new
               {
                   Mood = g.Key,
                   Count = g.Count(),
                   Avg = g.Average(x => x.MoodRating)
               })
               .OrderByDescending(g => g.Count)
               .ToList();
           int maxCount = grouped.Max(g => g.Count);
           int maxBlocks = 35;
           sw.WriteLine(string.Format("{0,-12} | {1,9} | {2,7} | Chart", "Mood", "Frequency", "AvgRate"));
           sw.WriteLine(new string('-', 70));
           foreach (var g in grouped)
           {
               int barLen = (int)Math.Round((double)g.Count / maxCount * maxBlocks);
               if (barLen == 0 && g.Count > 0) barLen = 1;
               string bar = new string('█', barLen);
               sw.WriteLine(string.Format("{0,-12} | {1,9} | {2,7:F1} | {3}", g.Mood, g.Count, g.Avg, bar));
           }
           sw.WriteLine();
       }
       // Console display version of the grouped table
       private void DisplayMoodTable(List<MoodEntry> list)
       {
           if (list.Count == 0)
           {
               Console.WriteLine("No data available.\n");
               return;
           }
           var grouped = list
               .GroupBy(e => string.IsNullOrWhiteSpace(e.Mood) ? "Unknown" : e.Mood)
               .Select(g => new
               {
                   Mood = g.Key,
                   Count = g.Count(),
                   Avg = g.Average(x => x.MoodRating)
               })
               .OrderByDescending(g => g.Count)
               .ToList();
           int maxCount = grouped.Max(g => g.Count);
           int maxBlocks = 35;
           Console.WriteLine("{0,-12} | {1,9} | {2,7} | Chart", "Mood", "Frequency", "AvgRate");
           Console.WriteLine(new string('-', 70));
           foreach (var g in grouped)
           {
               int barLen = (int)Math.Round((double)g.Count / maxCount * maxBlocks);
               if (barLen == 0 && g.Count > 0) barLen = 1;
               string bar = new string('█', barLen);
               Console.WriteLine("{0,-12} | {1,9} | {2,7:F1} | {3}",
                   g.Mood, g.Count, g.Avg, bar);
           }
           Console.WriteLine();
       }
       private static DateTime ParseDateSafe(string dateText)
       {
           if (string.IsNullOrWhiteSpace(dateText)) return DateTime.MinValue;
           string[] formats =
           {
               "MM/dd/yyyy hh:mm tt",
               "M/d/yyyy hh:mm tt",
               "MM/dd/yyyy h:mm tt",
               "M/d/yyyy h:mm tt",
               "MM/dd/yyyy",
               "M/d/yyyy"
           };
           if (DateTime.TryParseExact(dateText, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
               return parsed;
           if (DateTime.TryParse(dateText, out parsed))
               return parsed;
           return DateTime.MinValue;
       }
   }
   class UserAccount
   {
       private const string UserFile = "users.txt";
       public string LoggedInUser { get; private set; } = "";
       public bool Register()
       {
           Console.Clear();
           Console.Write("Enter University Email: ");
           string username = Console.ReadLine() ?? "";
           Console.Write("Create password: ");
           string password = Console.ReadLine() ?? ""; // not masked on register 
           File.AppendAllText(UserFile, $"{username}|{password}{Environment.NewLine}");
           Console.Clear();
           Console.WriteLine("✅ Registration successful!");
           // CONSENT AGREEMENT SECTION
           Console.WriteLine("\nNON-DISCLOSURE & CONSENT AGREEMENT");
           Console.WriteLine("All disclosed information from you will be kept as highly confidential data that will not be used against you.");
           Console.WriteLine("Your data will be used only for guidance associates' reference for future mental health practices.");
           Console.WriteLine("Nothing is shared externally.\n");
           Console.WriteLine("Do you agree?");
           Console.WriteLine("1. I Agree");
           Console.WriteLine("2. I Disagree");
           Console.Write("Choose: ");
           string consent = Console.ReadLine()?.Trim() ?? "";
           if (consent == "1")
           {
               Console.WriteLine("\nThank You! You may now continue.");
               Utils.Pause();
               return true;
           }
           else if (consent == "2")
           {
               Console.Clear();
               Console.WriteLine("Before you leave...");
               Console.WriteLine("\nDo you wish to talk to someone?");
               Console.WriteLine("1. Yes");
               Console.WriteLine("2. No");
               Console.Write("Choose: ");
               string talk = Console.ReadLine()?.Trim() ?? "";
               if (talk == "1")
               {
                   GuidanceHelpManager help = new GuidanceHelpManager();
                   help.DisplayHelp();
               }
               else
               {
                   Console.Clear();
                   Console.WriteLine("🌟 You matter.");
                   Console.WriteLine("You are strong, capable, and worthy.");
                   Console.WriteLine("We respect your choice. Take care!");
                   Utils.Pause();
                   Environment.Exit(0);
               }
           }
           else
           {
               Console.WriteLine("Invalid option. Returning...");
               Utils.Pause();
           }
           return true;
       }
       public bool Login()
       {
           Console.Clear();
           if (!File.Exists(UserFile))
           {
               Console.WriteLine("⚠ No accounts found. Please register first.");
               Utils.Pause();
               return false;
           }
           int attempts = 0;
           const int maxAttempts = 3;
           while (attempts < maxAttempts)
           {
               Console.Write("Username: ");
               string username = Console.ReadLine()?.Trim() ?? "";
               if (string.IsNullOrWhiteSpace(username))
               {
                   Console.WriteLine("⚠ Username cannot be empty.\n");
                   attempts++;
                   continue;
               }
               Console.Write("Password: ");
               string password = ReadHiddenPassword();
               if (string.IsNullOrWhiteSpace(password))
               {
                   Console.WriteLine("⚠ Password cannot be empty.\n");
                   attempts++;
                   continue;
               }
               bool userFound = false;
               foreach (var line in File.ReadAllLines(UserFile))
               {
                   var parts = line.Split('|');
                   // Skip malformed lines
                   if (parts.Length != 2)
                       continue;
                   if (parts[0] == username)
                   {
                       userFound = true;
                       if (parts[1] == password)
                       {
                           LoggedInUser = username;
                           Console.Clear();
                           Console.WriteLine($"🎉 Welcome back, {username}!");
                           Utils.Pause();
                           return true;
                       }
                       else
                       {
                           Console.WriteLine("❌ Incorrect password.\n");
                           attempts++;
                           break;
                       }
                   }
               }
               if (!userFound)
               {
                   Console.WriteLine("❌ Username does not exist.\n");
                   attempts++;
               }
           }
           Console.WriteLine("⛔ Too many failed login attempts. Try again later.");
           Utils.Pause();
           return false;
       }
       private string ReadHiddenPassword()
       {
           string pass = "";
           ConsoleKeyInfo key;
           while (true)
           {
               key = Console.ReadKey(true);
               if (key.Key == ConsoleKey.Enter)
               {
                   Console.WriteLine();
                   break;
               }
               else if (key.Key == ConsoleKey.Backspace && pass.Length > 0)
               {
                   pass = pass[..^1];
                   Console.Write("\b \b");
               }
               else if (!char.IsControl(key.KeyChar))
               {
                   pass += key.KeyChar;
                   Console.Write("*");
               }
           }
           return pass;
       }
   }
   class GuidanceHelpManager
   {
       public void DisplayHelp()
       {
           Console.Clear();
           Console.WriteLine("SUPPORT & GUIDANCE CENTER\n");
           Console.WriteLine("Do you wish to talk to someone?");
           Console.WriteLine("1. Yes");
           Console.WriteLine("2. No");
           Console.Write("\nChoose: ");
           string choice = Console.ReadLine()?.Trim() ?? "";
           if (choice == "1")
           {
               Console.Clear();
               Console.WriteLine("📞 HOTLINES & EMERGENCY CONTACTS\n");
               Console.WriteLine("National Center for Mental Health:");
               Console.WriteLine("• 0917-899-8727 (Globe/TM)");
               Console.WriteLine("• 0908-639-2672 (Smart/TNT)");
               Console.WriteLine("• 1553 (Landline)\n");
               Console.WriteLine("HOPELINE:");
               Console.WriteLine("• 0917-558-4673 (Globe/TM)");
               Console.WriteLine("• 0918-873-4673 (Smart/TNT)\n");
               Console.WriteLine("In Touch Crisis Line:");
               Console.WriteLine("• 0917-800-1123\n");
               Console.WriteLine("CIT-U Guidance Office:");
               Console.WriteLine("• 411-2000 local 132\n");
               Utils.Pause();
               return;
           }
           else if (choice == "2")
           {
               Console.Clear();
               Console.WriteLine("You are not alone.");
               Console.WriteLine("Your feelings are valid.");
               Console.WriteLine("You deserve peace and healing.");
               Console.WriteLine("Thank you for being here. \n");
               Utils.Pause();
               return;
           }
           else
           {
               Console.WriteLine("\nInvalid selection.");
               Utils.Pause();
           }
       }
   }
   class Program
   {
       static void Main()
       {
           UserAccount user = new();
           bool loggedIn = false;
           while (!loggedIn)
           {
               Console.WriteLine("1. Register\n2. Login\n3. Exit");
               Console.Write("Choose: ");
               string choice = Console.ReadLine() ?? "";
               switch (choice)
               {
                   case "1": user.Register(); break;
                   case "2": loggedIn = user.Login(); break;
                   case "3": return;
                   default: Console.Clear(); break;
               }
           }
           JournalManager manager = new(user.LoggedInUser);
           GuidanceHelpManager help = new();
           bool running = true;
           while (running)
           {
               Console.WriteLine("\n MAIN MENU ");
               Console.WriteLine("1. Add Journal Entry");
               Console.WriteLine("2. View Entries");
               Console.WriteLine("3. Edit Entry");
               Console.WriteLine("4. Delete Entry");
               Console.WriteLine("5. Mood Report");
               Console.WriteLine("6. Help Info");
               Console.WriteLine("7. Exit");
               Console.Write("Choose: ");
               string option = Console.ReadLine() ?? "";
               switch (option)
               {
                   case "1": manager.AddEntry(); break;
                   case "2": manager.ViewEntries(); break;
                   case "3": manager.EditEntry(); break;
                   case "4": manager.DeleteEntry(); break;
                   case "5": manager.ShowMoodReport(); break;
                   case "6": help.DisplayHelp(); break;
                   case "7": running = false; break;
                   default: Console.Clear(); break;
               }
           }
           Console.Clear();
           Console.WriteLine("Goodbye! 🌿");
       }
   }
}