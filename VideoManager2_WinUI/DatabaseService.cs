using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace VideoManager2_WinUI
{
    public class DatabaseService
    {
        private string _dbPath = "";
        public string DbPath => _dbPath;

        public async Task ConnectAsync(string dbPath)
        {
            _dbPath = dbPath;
            System.Diagnostics.Debug.WriteLine($"Database path set to: {_dbPath}");

            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await connection.OpenAsync();
                await CreateTablesIfNotExistsAsync(connection);
            }
        }

        private async Task CreateTablesIfNotExistsAsync(SqliteConnection connection)
        {
            var createFilesTableCommand = connection.CreateCommand();
            createFilesTableCommand.CommandText =
            @"
                CREATE TABLE IF NOT EXISTS Files (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FilePath TEXT NOT NULL UNIQUE,
                    FileName TEXT NOT NULL,
                    FileSize INTEGER NOT NULL,
                    DateModified TEXT NOT NULL,
                    Duration INTEGER NOT NULL
                );
            ";
            await createFilesTableCommand.ExecuteNonQueryAsync();

            var createTagsTableCommand = connection.CreateCommand();
            createTagsTableCommand.CommandText =
            @"
                CREATE TABLE IF NOT EXISTS Tags (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Color TEXT,
                    ParentId INTEGER,
                    FOREIGN KEY (ParentId) REFERENCES Tags(Id) ON DELETE CASCADE
                );
            ";
            await createTagsTableCommand.ExecuteNonQueryAsync();

            var createFileTagMapTableCommand = connection.CreateCommand();
            createFileTagMapTableCommand.CommandText =
            @"
                CREATE TABLE IF NOT EXISTS FileTagMap (
                    FileId INTEGER NOT NULL,
                    TagId INTEGER NOT NULL,
                    PRIMARY KEY (FileId, TagId),
                    FOREIGN KEY (FileId) REFERENCES Files(Id) ON DELETE CASCADE,
                    FOREIGN KEY (TagId) REFERENCES Tags(Id) ON DELETE CASCADE
                );
            ";
            await createFileTagMapTableCommand.ExecuteNonQueryAsync();
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
                    command.CommandText =
                    @"
                        INSERT OR IGNORE INTO Files (FilePath, FileName, FileSize, DateModified, Duration)
                        VALUES ($filePath, $fileName, $fileSize, $dateModified, $duration);
                    ";
                    
                    var filePathParam = command.CreateParameter();
                    filePathParam.ParameterName = "$filePath";

                    var fileNameParam = command.CreateParameter();
                    fileNameParam.ParameterName = "$fileName";

                    var fileSizeParam = command.CreateParameter();
                    fileSizeParam.ParameterName = "$fileSize";

                    var dateModifiedParam = command.CreateParameter();
                    dateModifiedParam.ParameterName = "$dateModified";

                    var durationParam = command.CreateParameter();
                    durationParam.ParameterName = "$duration";

                    command.Parameters.AddRange(new[] { filePathParam, fileNameParam, fileSizeParam, dateModifiedParam, durationParam });

                    foreach (var item in videoItems)
                    {
                        filePathParam.Value = item.FilePath;
                        fileNameParam.Value = item.FileName;
                        fileSizeParam.Value = item.FileSize;
                        dateModifiedParam.Value = item.DateModified.ToString("o");
                        durationParam.Value = item.Duration.Ticks;
                        await command.ExecuteNonQueryAsync();
                    }
                    await transaction.CommitAsync();
                }
            }
        }

        public async Task<List<VideoItem>> GetFilesAsync()
        {
            if (string.IsNullOrEmpty(_dbPath)) throw new InvalidOperationException("Database is not connected.");

            var videoItems = new List<VideoItem>();
            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Id, FilePath, FileName, FileSize, DateModified, Duration FROM Files";

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var videoItem = new VideoItem(
                            reader.GetString(1),
                            reader.GetString(2),
                            (ulong)reader.GetInt64(3),
                            DateTimeOffset.Parse(reader.GetString(4)),
                            TimeSpan.FromTicks(reader.GetInt64(5))
                        )
                        {
                            Id = reader.GetInt32(0)
                        };
                        videoItems.Add(videoItem);
                    }
                }
            }
            return videoItems;
        }

        /// <summary>
        /// データベースから現在のライブラリのファイル情報をすべて削除する
        /// </summary>
        public async Task ClearLibraryDataAsync()
        {
            if (string.IsNullOrEmpty(_dbPath)) throw new InvalidOperationException("Database is not connected.");

            using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                // ファイル情報と、ファイルとタグの関連付け情報を削除する。タグ自体は残す。
                command.CommandText = "DELETE FROM FileTagMap; DELETE FROM Files;";
                await command.ExecuteNonQueryAsync();
                System.Diagnostics.Debug.WriteLine("Cleared Files and FileTagMap tables.");
            }
        }
    }
}
