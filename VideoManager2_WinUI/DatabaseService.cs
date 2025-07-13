using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace VideoManager2_WinUI
{
    public class DatabaseService
    {
        private string _dbPath = "";
        public string DbPath => _dbPath;

        public async Task ConnectAsync(string dbPath)
        {
            _dbPath = dbPath;
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await connection.OpenAsync();
                // 外部キー制約を有効にする
                var foreignKeyCommand = connection.CreateCommand();
                foreignKeyCommand.CommandText = "PRAGMA foreign_keys = ON;";
                await foreignKeyCommand.ExecuteNonQueryAsync();

                await CreateTablesIfNotExistsAsync(connection);
            }
        }

        private async Task CreateTablesIfNotExistsAsync(SqliteConnection connection)
        {
            // (このメソッドに変更はありません)
            var createFilesTableCommand = connection.CreateCommand();
            createFilesTableCommand.CommandText = @"CREATE TABLE IF NOT EXISTS Files (Id INTEGER PRIMARY KEY AUTOINCREMENT, FilePath TEXT NOT NULL UNIQUE, FileName TEXT NOT NULL, IsFolder INTEGER NOT NULL DEFAULT 0, FileSize INTEGER NOT NULL, DateModified TEXT NOT NULL, Duration INTEGER NOT NULL);";
            await createFilesTableCommand.ExecuteNonQueryAsync();
            var createTagsTableCommand = connection.CreateCommand();
            createTagsTableCommand.CommandText = @"CREATE TABLE IF NOT EXISTS Tags (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL UNIQUE, Color TEXT, ParentId INTEGER, FOREIGN KEY (ParentId) REFERENCES Tags(Id) ON DELETE CASCADE);";
            await createTagsTableCommand.ExecuteNonQueryAsync();
            var createFileTagMapTableCommand = connection.CreateCommand();
            createFileTagMapTableCommand.CommandText = @"CREATE TABLE IF NOT EXISTS FileTagMap (FileId INTEGER NOT NULL, TagId INTEGER NOT NULL, PRIMARY KEY (FileId, TagId), FOREIGN KEY (FileId) REFERENCES Files(Id) ON DELETE CASCADE, FOREIGN KEY (TagId) REFERENCES Tags(Id) ON DELETE CASCADE);";
            await createFileTagMapTableCommand.ExecuteNonQueryAsync();
        }
        
        // (GetFilesAsync, AddOrUpdateFilesAsync, ValidateLibraryAsync, ClearLibraryDataAsync に変更はありません)
        public async Task<List<VideoItem>> GetFilesAsync()
        {
            if (string.IsNullOrEmpty(_dbPath)) throw new InvalidOperationException("Database is not connected.");
            var videoItems = new List<VideoItem>();
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Id, FilePath, FileName, IsFolder, FileSize, DateModified, Duration FROM Files";
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var videoItem = new VideoItem(reader.GetString(1), reader.GetString(2), reader.GetInt32(3) == 1, (ulong)reader.GetInt64(4), DateTimeOffset.Parse(reader.GetString(5)), TimeSpan.FromTicks(reader.GetInt64(6))) { Id = reader.GetInt32(0) };
                        videoItems.Add(videoItem);
                    }
                }
            }
            return videoItems;
        }
        public async Task AddOrUpdateFilesAsync(IEnumerable<VideoItem> videoItems)
        {
            if (string.IsNullOrEmpty(_dbPath)) throw new InvalidOperationException("Database is not connected.");
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    var command = connection.CreateCommand();
                    command.CommandText = @"INSERT OR IGNORE INTO Files (FilePath, FileName, IsFolder, FileSize, DateModified, Duration) VALUES ($filePath, $fileName, $isFolder, $fileSize, $dateModified, $duration);";
                    var filePathParam = command.CreateParameter(); filePathParam.ParameterName = "$filePath";
                    var fileNameParam = command.CreateParameter(); fileNameParam.ParameterName = "$fileName";
                    var isFolderParam = command.CreateParameter(); isFolderParam.ParameterName = "$isFolder";
                    var fileSizeParam = command.CreateParameter(); fileSizeParam.ParameterName = "$fileSize";
                    var dateModifiedParam = command.CreateParameter(); dateModifiedParam.ParameterName = "$dateModified";
                    var durationParam = command.CreateParameter(); durationParam.ParameterName = "$duration";
                    command.Parameters.AddRange(new[] { filePathParam, fileNameParam, isFolderParam, fileSizeParam, dateModifiedParam, durationParam });
                    foreach (var item in videoItems)
                    {
                        filePathParam.Value = item.FilePath; fileNameParam.Value = item.FileName; isFolderParam.Value = item.IsFolder ? 1 : 0; fileSizeParam.Value = item.FileSize; dateModifiedParam.Value = item.DateModified.ToString("o"); durationParam.Value = item.Duration.Ticks;
                        await command.ExecuteNonQueryAsync();
                    }
                    await transaction.CommitAsync();
                }
            }
        }
        public async Task<int> ValidateLibraryAsync()
        {
            if (string.IsNullOrEmpty(_dbPath)) throw new InvalidOperationException("Database is not connected.");
            var nonExistentItemIds = new List<long>();
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Id, FilePath, IsFolder FROM Files";
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var id = reader.GetInt64(0); var path = reader.GetString(1); var isFolder = reader.GetInt32(2) == 1;
                        bool exists = isFolder ? Directory.Exists(path) : File.Exists(path);
                        if (!exists) { nonExistentItemIds.Add(id); }
                    }
                }
                if (nonExistentItemIds.Any())
                {
                    var idList = string.Join(",", nonExistentItemIds);
                    var deleteFilesCommand = connection.CreateCommand(); deleteFilesCommand.CommandText = $"DELETE FROM Files WHERE Id IN ({idList})";
                    await deleteFilesCommand.ExecuteNonQueryAsync();
                    var deleteMapCommand = connection.CreateCommand(); deleteMapCommand.CommandText = $"DELETE FROM FileTagMap WHERE FileId IN ({idList})";
                    await deleteMapCommand.ExecuteNonQueryAsync();
                }
            }
            return nonExistentItemIds.Count;
        }
        public async Task ClearLibraryDataAsync()
        {
            if (string.IsNullOrEmpty(_dbPath)) throw new InvalidOperationException("Database is not connected.");
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM FileTagMap; DELETE FROM Files;";
                await command.ExecuteNonQueryAsync();
            }
        }
        public async Task<List<Tag>> GetTagsAsync()
        {
            if (string.IsNullOrEmpty(_dbPath)) throw new InvalidOperationException("Database is not connected.");
            var tags = new Dictionary<int, Tag>();
            var rootTags = new List<Tag>();
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Id, Name, Color, ParentId FROM Tags";
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var tag = new Tag(reader.GetString(1)) { Id = reader.GetInt32(0), Color = reader.IsDBNull(2) ? null : reader.GetString(2), ParentId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3) };
                        tags.Add(tag.Id, tag);
                    }
                }
            }
            foreach (var tag in tags.Values)
            {
                if (tag.ParentId.HasValue && tags.TryGetValue(tag.ParentId.Value, out var parentTag)) { parentTag.Children.Add(tag); } else { rootTags.Add(tag); }
            }
            return rootTags;
        }

        // ★★★ ここから新規追加/修正 ★★★

        /// <summary>
        /// 新しいタグをデータベースに追加する
        /// </summary>
        public async Task AddTagAsync(string name, int? parentId)
        {
            if (string.IsNullOrEmpty(_dbPath)) throw new InvalidOperationException("Database is not connected.");

            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO Tags (Name, ParentId) VALUES ($name, $parentId)";
                command.Parameters.AddWithValue("$name", name);
                command.Parameters.AddWithValue("$parentId", parentId ?? (object)DBNull.Value); // parentIdがnullならDBNullを設定
                await command.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// 既存のタグ情報を更新する
        /// </summary>
        public async Task UpdateTagAsync(Tag tag)
        {
            if (string.IsNullOrEmpty(_dbPath)) throw new InvalidOperationException("Database is not connected.");

            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                // 今回は名前の変更のみ対応
                command.CommandText = "UPDATE Tags SET Name = $name WHERE Id = $id";
                command.Parameters.AddWithValue("$name", tag.Name);
                command.Parameters.AddWithValue("$id", tag.Id);
                await command.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// タグをデータベースから削除する
        /// </summary>
        public async Task DeleteTagAsync(Tag tag)
        {
            if (string.IsNullOrEmpty(_dbPath)) throw new InvalidOperationException("Database is not connected.");

            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM Tags WHERE Id = $id";
                command.Parameters.AddWithValue("$id", tag.Id);
                await command.ExecuteNonQueryAsync();
                // テーブル作成時に ON DELETE CASCADE を設定しているため、
                // 子タグとFileTagMapの関連レコードは自動的に削除される。
            }
        }
    }
}