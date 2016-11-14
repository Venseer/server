﻿#region

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#endregion

namespace UlteriusServer.Utilities.Extensions
{
    /// <summary>
    ///     Enum FormatTokenFlags
    /// </summary>
    public enum FormatTokenFlags
    {
        /// <summary>
        ///     Uses the specifier token as the replacement token, matches '%' but not '%%'
        /// </summary>
        SpecifierToken,

        /// <summary>
        ///     Like String.Format
        /// </summary>
        IndexToken,

        /// <summary>
        ///     Extracts an Object's Members by name.
        /// </summary>
        MemberToken
    }

    /// <summary>
    ///     Class StringExtensions
    /// </summary>
    public static class StringExtensions
    {

        // Returns the human-readable file totalSize for an arbitrary, 64-bit file totalSize 
        // The default format is "0.### XB", e.g. "4.2 KB" or "1.434 GB"

        public static string UnicodeUtf8(this string strFrom)
        {
            var bytSrc = Encoding.Unicode.GetBytes(strFrom);
            var bytDestination = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, bytSrc);
            var strTo = Encoding.UTF8.GetString(bytDestination);
            return strTo;
        }


        public static string GetBytesReadable(long i)
        {
            // Get absolute value
            var absoluteI = i < 0 ? -i : i;
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (absoluteI >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = i >> 50;
            }
            else if (absoluteI >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = i >> 40;
            }
            else if (absoluteI >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = i >> 30;
            }
            else if (absoluteI >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = i >> 20;
            }
            else if (absoluteI >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = i >> 10;
            }
            else if (absoluteI >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = i;
            }
            else
            {
                return i.ToString("0 B"); // Byte
            }
            // Divide by 1024 to get fractional value
            readable = readable / 1024;
            // Return formatted number with suffix
            return readable.ToString("0.### ") + suffix;
        }

        /// <summary>
        /// Returns a Secure string from the source string
        /// </summary>
        /// <param name="Source"></param>
        /// <returns></returns>
        public static SecureString ToSecureString(this string Source)
        {
            if (string.IsNullOrWhiteSpace(Source))
                return null;
            var Result = new SecureString();
            foreach (var c in Source.ToCharArray())
                Result.AppendChar(c);
            return Result;
        }
        public static string ToUnsecureString(this SecureString secureString)
        {
            if (secureString == null) throw new ArgumentNullException("secureString");

            var unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(secureString);
                return Marshal.PtrToStringUni(unmanagedString);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }


        /// <summary>
        ///     The Reformatting function called to format the string with a list of arguments.
        /// </summary>
        private static Func<Func<string, object>, string, IList<object>, string> _regexReformatter;

        /// <summary>
        ///     The Regex which matches the specifier token in strings.
        /// </summary>
        private static Regex _specifierTokenRegex;

        /// <summary>
        ///     The Regex which matches parameter indexes in strings, same as string.Format.
        /// </summary>
        private static Regex _argumentIndexRegex;

        /// <summary>
        ///     The Regex which matches object member names in strings.
        /// </summary>
        private static Regex _objectMemberRegex;

        /// <summary>
        ///     Splits a string into an array of strings, the string is split by commas.
        /// </summary>
        /// <param name="this">The string instance to split by commas.</param>
        /// <returns>The <paramref name="this" /> string that has been comma separated into substrings.</returns>
        public static string[] CommaSeparate(this string @this)
        {
            return @this.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        ///     Determines whether two String objects contain the same data, ignoring the case of the letters in the String; uses
        ///     Ordinal comparison.
        /// </summary>
        /// <param name="this">The current string to be compared to.</param>
        /// <param name="other">The other string to compare against the current String for equality.</param>
        /// <returns><c>true</c> if the two strings are equal, <c>false</c> otherwise</returns>
        public static bool EqualsIgnoreCase(this string @this, string other)
        {
            return string.Equals(@this, other, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsBase64String(this string s)
        {
            s = s.Trim();
            return (s.Length%4 == 0) && Regex.IsMatch(s, @"^[a-zA-Z0-9\+/]*={0,3}$", RegexOptions.None);
        }

        public static bool IsValidJson(this string strInput)
        {
            strInput = strInput.Trim();
            if ((strInput.StartsWith("{") && strInput.EndsWith("}")) || //For object
                (strInput.StartsWith("[") && strInput.EndsWith("]"))) //For array
            {
                try
                {
                    JToken.Parse(strInput);
                    return true;
                }
                catch (JsonReaderException)
                {
                    //Exception in parsing json
                    return false;
                }
                catch (Exception) //some other exception
                {
                    return false;
                }
            }
            return false;
        }


        /// <summary>
        ///     Replaces one or more format items in a specified string with the string representation of a specified object.
        /// </summary>
        /// <returns>
        ///     A copy of <paramref name="format" /> in which any format items are replaced by the string representation of
        ///     <paramref name="values" />.
        /// </returns>
        /// <param name="format">A composite format string. </param>
        /// <param name="values">The arguments to use in formatting <paramref name="format" />.</param>
        public static string Form(this string format, params object[] values)
        {
            var specifierTokenMatches = _specifierTokenRegex.Matches(format);
            var indexTokenMatches = _argumentIndexRegex.Matches(format);
            var memberNameMatches = _objectMemberRegex.Matches(format);

            if (memberNameMatches.Count > 0 && values.Length == 1)
            {
                format = FormatString(format, FormatTokenFlags.MemberToken, values);
            }
            else
            {
                if (indexTokenMatches.Count > 0)
                {
                    format = FormatString(format, FormatTokenFlags.IndexToken, values);
                }

                if (specifierTokenMatches.Count > 0)
                {
                    format = FormatString(format, FormatTokenFlags.SpecifierToken, values);
                }
            }
            return format;
        }

        public static string CreateMD5(this string input)
        {
            // Use input string to calculate MD5 hash
            using (var md5 = MD5.Create())
            {
                var inputBytes = Encoding.ASCII.GetBytes(input);
                var hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                var sb = new StringBuilder();
                foreach (var t in hashBytes)
                {
                    sb.Append(t.ToString("X2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        ///     The Function which does all the heavy lifting.
        /// </summary>
        /// <param name="format">A composite format string</param>
        /// <param name="flags">The flags which specify how the <paramref name="format" /> string should be interpreted.</param>
        /// <param name="arguments">The arguments to format the <paramref name="format" /> parameter with.</param>
        /// <returns>System.String.</returns>
        public static string FormatString(string format, FormatTokenFlags flags, params object[] arguments)
        {
            switch (flags)
            {
                case FormatTokenFlags.IndexToken:
                    return string.Format(format, arguments);

                case FormatTokenFlags.SpecifierToken:
                    var stringBuilder = new StringBuilder();
                    for (int i = 0,
                        argIndex = 0;
                        i < format.Length;
                        i++)
                    {
                        stringBuilder.Append(format[i] == '%' && argIndex < arguments.Length
                            ? "{" + argIndex++ + "}"
                            : format.Substring(i, 1));
                    }
                    return string.Format(stringBuilder.ToString(), arguments);

                case FormatTokenFlags.MemberToken:
                    return
                        string.Format(
                            _regexReformatter(name => name == "0" ? arguments[0] : DataBinder.Eval(arguments[0], name),
                                format, arguments), arguments);

                default:
                    return format;
            }
        }

        public static void Initialize()
        {
            const RegexOptions regexOptions = RegexOptions.Compiled | RegexOptions.CultureInvariant;
            _objectMemberRegex = new Regex(@"(?<start>(\{))+(?<property>[\w\.]+)(?<end>(\}))+", regexOptions);
            _specifierTokenRegex = new Regex(@"(?<!\\)%", regexOptions);
            _argumentIndexRegex = new Regex(@"\{(\d)\}", regexOptions);
            _regexReformatter = (valueFetcher, format, parameters) =>
            {
                var argumentCollection = new List<object>();
                var rewrittenFormat = _objectMemberRegex.Replace(format, match =>
                {
                    Group startGroup = match.Groups["start"],
                        propertyGroup = match.Groups["property"],
                        endGroup = match.Groups["end"];

                    var result = valueFetcher(propertyGroup.Value);

                    argumentCollection.Add(result);
                    var index = argumentCollection.Count - 1;
                    var fmt = new string('{', startGroup.Captures.Count) + index +
                              new string('}', endGroup.Captures.Count);
                    return string.Format(fmt, argumentCollection.ToArray());
                });

                return rewrittenFormat;
            };
        }
    }
}