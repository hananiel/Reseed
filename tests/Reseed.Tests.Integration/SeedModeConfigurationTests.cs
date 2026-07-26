using System;
using System.Collections.Generic;
using NUnit.Framework;
using Reseed.Configuration;
using Reseed.Configuration.Basic;
using Reseed.Configuration.Cleanup;
using Reseed.Configuration.TemporaryTables;
using Reseed.Data;

namespace Reseed.Tests.Integration
{
	public sealed class SeedModeConfigurationTests
	{
		private static readonly IDataProvider DataProvider = new EmptyDataProvider();

		[Test]
		public void ShouldAcceptNoCleanupForEverySeedMode()
		{
			var noCleanup = CleanupDefinition.NoCleanup();

			Assert.Multiple(() =>
			{
				Assert.That(
					() => SeedMode.Basic(BasicInsertDefinition.Script(), noCleanup, DataProvider),
					Throws.Nothing);
				Assert.That(
					() => SeedMode.TemporaryTables(
						"temp",
						TemporaryTablesInsertDefinition.Script(),
						noCleanup,
						DataProvider),
					Throws.Nothing);
				Assert.That(
					() => SeedMode.CleanupOnly(noCleanup),
					Throws.Nothing);
			});
		}

		private sealed class EmptyDataProvider : IDataProvider
		{
			public IReadOnlyCollection<Entity> GetEntities() => Array.Empty<Entity>();
		}
	}
}
