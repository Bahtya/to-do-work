using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using Todowork.Models;

namespace Todowork.Services
{
    public sealed class TodoRepository
    {
        private readonly string _filePath;

        public TodoRepository(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public List<TodoItem> Load()
        {
            if (!File.Exists(_filePath))
            {
                return new List<TodoItem>();
            }

            try
            {
                using (var stream = File.OpenRead(_filePath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(List<TodoItem>));
                    var result = serializer.ReadObject(stream) as List<TodoItem>;
                    return result ?? new List<TodoItem>();
                }
            }
            catch
            {
                try
                {
                    var backupPath = _filePath + ".bad_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";
                    File.Copy(_filePath, backupPath, true);
                }
                catch { }

                return new List<TodoItem>();
            }
        }

        public void Save(IReadOnlyList<TodoItem> items)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tmpPath = _filePath + ".tmp";

            using (var stream = File.Create(tmpPath))
            {
                var serializer = new DataContractJsonSerializer(typeof(List<TodoItem>));
                serializer.WriteObject(stream, new List<TodoItem>(items));
            }

            try
            {
                if (File.Exists(_filePath))
                {
                    File.Replace(tmpPath, _filePath, null);
                }
                else
                {
                    File.Move(tmpPath, _filePath);
                }
            }
            catch
            {
                File.Copy(tmpPath, _filePath, true);
                File.Delete(tmpPath);
            }
        }
    }
}
