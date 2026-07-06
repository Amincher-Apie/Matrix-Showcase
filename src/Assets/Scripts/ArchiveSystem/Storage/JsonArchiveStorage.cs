using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ArchiveSystem
{
	/// <summary>
	/// 使用 <see cref="JsonManager"/> 将档案序列化到本地 JSON 文件的具体实现。
	/// </summary>
	public class JsonArchiveStorage : IArchiveStorage
	{
		private const string RootFolder = "Archives";
		private const string FileSuffix = "_archive";
		private const int MaxBackupCount = 5;

		/// <summary>文件写入锁，避免并发写导致损坏。</summary>
		private readonly object _fileLock = new object();

		/// <inheritdoc />
		public PlayerArchiveData Load(string playerId)
		{
			string token = BuildFileToken(playerId);
			string fullPath = GetArchiveFilePath(playerId);
			
			Debug.Log($"[JsonArchiveStorage] 加载存档 - PlayerId: {playerId}, Token: {token}");
			Debug.Log($"[JsonArchiveStorage] 完整文件路径: {fullPath}");
			Debug.Log($"[JsonArchiveStorage] PersistentDataPath: {Application.persistentDataPath}");
			Debug.Log($"[JsonArchiveStorage] 文件是否存在: {File.Exists(fullPath)}");
			
			EnsureDirectory();
			return JsonManager.Instance.LoadData<PlayerArchiveData>(token);
		}

		/// <inheritdoc />
		public async Task<PlayerArchiveData> LoadAsync(string playerId, CancellationToken token = default)
		{
			string tokenName = BuildFileToken(playerId);
			EnsureDirectory();
			return await JsonManager.Instance.LoadDataAsync<PlayerArchiveData>(tokenName);
		}

		/// <inheritdoc />
		public void Save(PlayerArchiveData data)
		{
			if (data == null)
			{
				Debug.LogError("[JsonArchiveStorage] 保存数据失败：data 为空");
				return;
			}

			lock (_fileLock)
			{
				EnsureDirectory();
				JsonManager.Instance.SaveData(data, BuildFileToken(data.Meta.PlayerId));
				data.Meta.LastSaveTimeUtc = DateTime.UtcNow;
			}
		}

		/// <inheritdoc />
		public async Task SaveAsync(PlayerArchiveData data, CancellationToken token = default)
		{
			if (data == null)
			{
				Debug.LogError("[JsonArchiveStorage] 异步保存数据失败：data 为空");
				return;
			}

			lock (_fileLock)
			{
				EnsureDirectory();
			}

			await JsonManager.Instance.SaveDataAsync(data, BuildFileToken(data.Meta.PlayerId));
			data.Meta.LastSaveTimeUtc = DateTime.UtcNow;
		}

		/// <inheritdoc />
		public void Backup(PlayerArchiveData data, string reason = "")
		{
			lock (_fileLock)
			{
				string filePath = GetArchiveFilePath(data.Meta.PlayerId);
				if (!File.Exists(filePath))
				{
					return;
				}

				string timeStamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
				string backupName = $"{Path.GetFileNameWithoutExtension(filePath)}_{timeStamp}";
				if (!string.IsNullOrEmpty(reason))
				{
					backupName += $"_{reason}";
				}

				string backupPath = Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, backupName + ".json");
				File.Copy(filePath, backupPath, true);
				TrimBackups(Path.GetDirectoryName(filePath) ?? string.Empty, Path.GetFileNameWithoutExtension(filePath) ?? string.Empty);
			}
		}

		/// <summary>
		/// 构建用于 JsonManager 的文件 token（包含子目录和安全的文件名）。
		/// </summary>
		private static string BuildFileToken(string playerId)
		{
			if (string.IsNullOrEmpty(playerId))
			{
				playerId = "default";
			}

			foreach (char invalid in Path.GetInvalidFileNameChars())
			{
				playerId = playerId.Replace(invalid, '_');
			}

			return Path.Combine(RootFolder, $"{playerId}{FileSuffix}");
		}

		/// <summary>
		/// 获取本地完整文件路径。
		/// </summary>
		private static string GetArchiveFilePath(string playerId)
		{
			string token = BuildFileToken(playerId);
			return Path.Combine(Application.persistentDataPath, $"{token}.json");
		}

		/// <summary>
		/// 确保存档目录存在。
		/// </summary>
		private static void EnsureDirectory()
		{
			string path = Path.Combine(Application.persistentDataPath, RootFolder);
			if (!Directory.Exists(path))
			{
				Directory.CreateDirectory(path);
			}
		}

		/// <summary>
		/// 控制备份数量，防止目录无限膨胀。
		/// </summary>
		private static void TrimBackups(string directory, string baseName)
		{
			if (!Directory.Exists(directory))
			{
				return;
			}

			string[] backups = Directory.GetFiles(directory, $"{baseName}_*.json");
			if (backups.Length <= MaxBackupCount)
			{
				return;
			}

			Array.Sort(backups, StringComparer.OrdinalIgnoreCase);
			int removeCount = backups.Length - MaxBackupCount;
			for (int i = 0; i < removeCount; i++)
			{
				File.Delete(backups[i]);
			}
		}
	}
}

