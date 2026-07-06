using System.Threading;
using System.Threading.Tasks;

namespace ArchiveSystem
{
	/// <summary>
	/// 档案存储抽象，隔离不同介质（本地 JSON、数据库、云端等）的实现。
	/// </summary>
	public interface IArchiveStorage
	{
		/// <summary>同步加载玩家档案。</summary>
		PlayerArchiveData Load(string playerId);

		/// <summary>异步加载玩家档案。</summary>
		Task<PlayerArchiveData> LoadAsync(string playerId, CancellationToken token = default);

		/// <summary>同步保存玩家档案。</summary>
		void Save(PlayerArchiveData data);

		/// <summary>异步保存玩家档案。</summary>
		Task SaveAsync(PlayerArchiveData data, CancellationToken token = default);

		/// <summary>备份当前档案，便于回滚。</summary>
		void Backup(PlayerArchiveData data, string reason = "");
	}
}

