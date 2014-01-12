using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Octokit.Internal;
using GitHubSharp.Internal;

namespace GitHubSharp
{
	internal static class StringExtensions
	{
		// :trollface:
		[SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase",
			Justification = "Ruby don't care. Ruby don't play that.")]
		public static string ToRubyCase(this string propertyName)
		{
			return string.Join("_", propertyName.SplitUpperCase()).ToLowerInvariant();
		}

		static IEnumerable<string> SplitUpperCase(this string source)
		{
			int wordStartIndex = 0;
			var letters = source.ToCharArray();
			var previousChar = char.MinValue;

			// Skip the first letter. we don't care what case it is.
			for (int i = 1; i < letters.Length; i++)
			{
				if (char.IsUpper(letters[i]) && !char.IsWhiteSpace(previousChar))
				{
					//Grab everything before the current character.
					yield return new String(letters, wordStartIndex, i - wordStartIndex);
					wordStartIndex = i;
				}
				previousChar = letters[i];
			}

			//We need to have the last word.
			yield return new String(letters, wordStartIndex, letters.Length - wordStartIndex);
		}
	}

	public class SimpleJsonSerializer : IJsonSerializer
	{
		readonly GitHubSerializerStrategy _serializationStrategy = new GitHubSerializerStrategy();

		public string Serialize(object item)
		{
			return SimpleJson.SerializeObject(item, _serializationStrategy);
		}

		public T Deserialize<T>(string json)
		{
			return SimpleJson.DeserializeObject<T>(json, _serializationStrategy);
		}

		class GitHubSerializerStrategy : PocoJsonSerializerStrategy
		{
			protected override string MapClrMemberNameToJsonFieldName(string clrPropertyName)
			{
				return clrPropertyName.ToRubyCase();
			}

			// This is overridden so that null values are omitted from serialized objects.
			[SuppressMessage("Microsoft.Design", "CA1007:UseGenericsWhereAppropriate", Justification = "Need to support .NET 2")]
			protected override bool TrySerializeUnknownTypes(object input, out object output)
			{
				if (input == null) throw new ArgumentNullException("input");
				output = null;
				Type type = input.GetType();
				if (type.FullName == null)
					return false;
				IDictionary<string, object> obj = new JsonObject();
				IDictionary<string, ReflectionUtils.GetDelegate> getters = GetCache[type];
				foreach (KeyValuePair<string, ReflectionUtils.GetDelegate> getter in getters)
				{
					if (getter.Value != null)
					{
						var value = getter.Value(input);
//						if (value == null)
//							continue;

						obj.Add(MapClrMemberNameToJsonFieldName(getter.Key), value);
					}
				}
				output = obj;
				return true;
			}

			[SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase",
				Justification = "The API expects lowercase values")]
			protected override object SerializeEnum(Enum p)
			{
				return p.ToString().ToLowerInvariant();
			}

			// Overridden to handle enums.
			public override object DeserializeObject(object value, Type type)
			{
				var stringValue = value as string;
				if (stringValue != null)
				{
					if (ReflectionUtils.IsUri(type))
					{
						Uri result;
						if (Uri.TryCreate(stringValue, UriKind.RelativeOrAbsolute, out result))
						{
							return result;
						}
					}

					if (ReflectionUtils.GetTypeInfo(type).IsEnum)
					{
						// remove '-' from values coming in to be able to enum utf-8
						stringValue = stringValue.Replace("-", "");
						return Enum.Parse(type, stringValue, ignoreCase: true);
					}

					if (ReflectionUtils.IsNullableType(type))
					{
						var underlyingType = Nullable.GetUnderlyingType(type);
						if (ReflectionUtils.GetTypeInfo(underlyingType).IsEnum)
						{
							return Enum.Parse(underlyingType, stringValue, ignoreCase: true);
						}
					}
				}

				return base.DeserializeObject(value, type);
			}
		}
	}
}
