using System;
using System.Threading.Tasks;
using DbUp;
using DbUp.Engine;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Reseed.Samples.NUnit
{
	public class SqlServerContainer: IAsyncDisposable
	{
		private const string DatabaseName = "MsSqlContainerDb";
		private const string Password = "!A1B2c3d4_";
		private const string ServerImage = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

		private readonly string scriptsFolder;
		private readonly MsSqlContainer server;

		public string ConnectionString
		{
			get
			{
				var connectionString = new SqlConnectionStringBuilder(server.GetConnectionString())
				{
					InitialCatalog = DatabaseName,
					TrustServerCertificate = true
				};

				return connectionString.ConnectionString;
			}
		}

		public SqlServerContainer(string scriptsFolder)
		{
			this.scriptsFolder = scriptsFolder ?? throw new ArgumentNullException(nameof(scriptsFolder));
			this.server = CreateDatabaseContainer();
		}

		public async Task StartAsync()
		{
			await StartServerAsync();
			EnsureDatabase.For.SqlDatabase(ConnectionString);

			var migrationResult = MigrateDatabase(ConnectionString, scriptsFolder);
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

		private static DatabaseUpgradeResult MigrateDatabase(string connectionString, string scriptsPath)
		{
			var upgrade = DeployChanges.To
				.SqlDatabase(connectionString, "dbo")
				.WithScriptsFromFileSystem(scriptsPath)
				.LogToConsole()
				.WithTransactionPerScript()
				.Build();
			
			return upgrade.PerformUpgrade();
		}
	}
}
