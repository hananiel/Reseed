using System;
using System.Globalization;
using System.Xml;

namespace Reseed.Data.Providers.FileSystem
{
	public static class XmlValueTransformers
	{
		public static readonly Func<XmlValueContext, string, string> Duration =
			(_, value) =>
			{
				if (string.IsNullOrEmpty(value) ||
				    value[0] != 'P' && !(value[0] == '-' && value.Length > 1 && value[1] == 'P'))
				{
					return value;
				}

				try
				{
					return XmlConvert
						.ToTimeSpan(value)
						.ToString("c", CultureInfo.InvariantCulture);
				}
				catch (FormatException)
				{
					return value;
				}
			};
	}
}
