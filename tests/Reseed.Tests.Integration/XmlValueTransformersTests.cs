using NUnit.Framework;
using Reseed.Data.Providers.FileSystem;

namespace Reseed.Tests.Integration
{
	public sealed class XmlValueTransformersTests
	{
		[TestCase("P2DT3H4M5.678S", "2.03:04:05.6780000")]
		[TestCase("-PT1H30M", "-01:30:00")]
		[TestCase("PT is ordinary text", "PT is ordinary text")]
		public void ShouldConvertXmlSchemaDurationToTimeSpan(string value, string expected)
		{
			var context = new XmlValueContext("data.xml", "Entity", "Value");

			var result = XmlValueTransformers.Duration(context, value);

			Assert.That(result, Is.EqualTo(expected));
		}
	}
}
