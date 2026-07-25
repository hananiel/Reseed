using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using DbUp;
using DbUp.Engine;
using DbUp.Helpers;
using Testcontainers.MsSql;

namespace Reseed.Tests.Integration.Core
{
	public sealed class SqlServerContainer: IAsyncDisposable
	{
		private const string DatabaseName = "MsSqlContainerDb";
		private const string Password = "!A1B2c3d4_";
		private const string ServerImage = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

		private readonly string scriptsFolder;
		private readonly Func<string, bool> scriptFilter;
		private readonly MsSqlContainer server;

		public string ConnectionString
		{
			get
			{
				var connectionString = new SqlConnectionStringBuilder(server.GetConnectionString())
				{
					InitialCatalog = DatabaseName
				};

				return connectionString.ConnectionString;
			}
		}

		public SqlServerContainer(string scriptsFolder)
			: this(scriptsFolder, _ => true)
		{
		}

		public SqlServerContainer(string scriptsFolder, Func<string, bool> scriptFilter)
		{
			this.scriptsFolder = scriptsFolder ?? throw new ArgumentNullException(nameof(scriptsFolder));
			this.scriptFilter = scriptFilter ?? throw new ArgumentNullException(nameof(scriptFilter));
			this.server = CreateDatabaseContainer();
		}

		public async Task StartAsync()
		{
			await StartServerAsync();
			EnsureDatabase.For.SqlDatabase(ConnectionString);

			var migrationResult = MigrateDatabase(
				ConnectionString,
				scriptsFolder,
				scriptFilter);

			if (!migrationResult.Successful)
			{
				throw new InvalidOperationException("Can't apply database migrations", migrationResult.Error);
			}
		}

		private async Task StartServerAsync()
		{
			try
			{
				await server.StartAsync();
			}
			catch (TimeoutException ex)
			{
				throw new InvalidOperationException(
					"Can't start sql server container, make sure docker is running",
					ex);
			}
		}

		public ValueTask DisposeAsync() => 
			this.server.DisposeAsync();

		private static MsSqlContainer CreateDatabaseContainer() =>
			new MsSqlBuilder(ServerImage)
				.WithPassword(Password)
				.Build();

		private static DatabaseUpgradeResult MigrateDatabase(
			string connectionString,
			string scriptsPath,
			Func<string, bool> scriptFilter)
		{
			var upgrade = DeployChanges.To
				.SqlDatabase(connectionString, "dbo")
				.WithScriptsFromFileSystem(scriptsPath, scriptFilter)
				.LogToConsole()
				.JournalTo(new NullJournal())
				.WithTransactionPerScript()
				.Build();
			
			return upgrade.PerformUpgrade();
		}
	}
}
