// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text;
using System.IO;
using Xunit;

namespace System.Globalization.Tests
{
    public class StringNormalizationAllTests
    {
        private string ConvertToString(string codepoints)
        {
            StringBuilder sb = new StringBuilder();
            string[] parts = codepoints.Split('-');
            foreach (string part in parts)
            {
                sb.Append((char) int.Parse(part, NumberStyles.HexNumber | NumberStyles.AllowTrailingWhite | NumberStyles.AllowLeadingWhite, CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        [Fact]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/34577", TestPlatforms.Windows, TargetFrameworkMonikers.Netcoreapp, TestRuntimes.Mono)]
        public void Normalize()
        {
            // Windows 8 test data came from http://www.unicode.org/Public/UCD/latest/ucd/NormalizationTest.txt
            // Windows 7 test came from http://www.unicode.org/Public/3.0-Update1/NormalizationTest-3.0.1.txt

            using (Stream stream = typeof(StringNormalizationAllTests).GetTypeInfo().Assembly.GetManifestResourceStream(PlatformDetection.IsWindows7 ? "NormalizationDataWin7" : "NormalizationDataWin8"))
            {
                using (StreamReader sr = new StreamReader(stream))
                {
                    int index = 0;
                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine();
                        string[] parts = line.Split(',');
                        Assert.True(parts.Length == 5, $"Wrong data at the line {index}");

                        string part0 = ConvertToString(parts[0]);
                        string part1 = ConvertToString(parts[1]);
                        string part2 = ConvertToString(parts[2]);
                        string part3 = ConvertToString(parts[3]);
                        string part4 = ConvertToString(parts[4]);

                        // Form C
                        VerifyConformanceInvariant(NormalizationForm.FormC, part0, part1, part2, part3, part4);

                        // Form D
                        VerifyConformanceInvariant(NormalizationForm.FormD, part0, part1, part2, part3, part4);

                        // Mobile / Browser ICU doesn't support FormKC and FormKD
                        if (PlatformDetection.IsNotUsingLimitedCultures || PlatformDetection.IsHybridGlobalizationOnApplePlatform)
                        {
                            // Form KC
                            VerifyConformanceInvariant(NormalizationForm.FormKC, part0, part1, part2, part3, part4);

                            // Form KD
                            VerifyConformanceInvariant(NormalizationForm.FormKD, part0, part1, part2, part3, part4);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verifies the first normalization conformance invariant for the
        /// specified normalization form, where the rule as defined for all
        /// Unicode normalization forms is as follows:
        ///  1. The following invariants must be true for all conformant
        ///    implementations:
        ///    NFC
        ///      c2 ==  NFC(c1) ==  NFC(c2) ==  NFC(c3)
        ///      c4 ==  NFC(c4) ==  NFC(c5)
        ///
        ///    NFD
        ///      c3 ==  NFD(c1) ==  NFD(c2) ==  NFD(c3)
        ///      c5 ==  NFD(c4) ==  NFD(c5)
        ///
        ///    NFKC
        ///      c4 == NFKC(c1) == NFKC(c2) == NFKC(c3) == NFKC(c4) == NFKC(c5)
        ///
        ///    NFKD
        ///      c5 == NFKD(c1) == NFKD(c2) == NFKD(c3) == NFKD(c4) == NFKD(c5)
        /// </summary>
        private static void VerifyConformanceInvariant(NormalizationForm normForm, string c1, string c2, string c3, string c4, string c5)
        {
            string normalized1 = c1.Normalize(normForm);
            string normalized2 = c2.Normalize(normForm);
            string normalized3 = c3.Normalize(normForm);
            string normalized4 = c4.Normalize(normForm);
            string normalized5 = c5.Normalize(normForm);

            Span<char> normalizedSpan1 = new char[normalized1.Length];
            Span<char> normalizedSpan2 = new char[normalized2.Length];
            Span<char> normalizedSpan3 = new char[normalized3.Length];
            Span<char> normalizedSpan4 = new char[normalized4.Length];
            Span<char> normalizedSpan5 = new char[normalized5.Length];

            Assert.True(c1.AsSpan().TryNormalize(normalizedSpan1, out int charsWritten1, normForm), $"'{c1}' is not normalized with form {normForm}");
            Assert.True(c2.AsSpan().TryNormalize(normalizedSpan2, out int charsWritten2, normForm), $"'{c2}' is not normalized with form {normForm}");
            Assert.True(c3.AsSpan().TryNormalize(normalizedSpan3, out int charsWritten3, normForm), $"'{c3}' is not normalized with form {normForm}");
            Assert.True(c4.AsSpan().TryNormalize(normalizedSpan4, out int charsWritten4, normForm), $"'{c4}' is not normalized with form {normForm}");
            Assert.True(c5.AsSpan().TryNormalize(normalizedSpan5, out int charsWritten5, normForm), $"'{c5}' is not normalized with form {normForm}");

            switch (normForm)
            {
                case NormalizationForm.FormC:
                    // c2 ==  NFC(c1) ==  NFC(c2) ==  NFC(c3)
                    AssertEqualsForm(c2, normalized1);
                    AssertEqualsForm(c2, normalized2);
                    AssertEqualsForm(c2, normalized3);

                    AssertEqualsForm(c2, normalizedSpan1.Slice(0, charsWritten1).ToString());
                    AssertEqualsForm(c2, normalizedSpan2.Slice(0, charsWritten2).ToString());
                    AssertEqualsForm(c2, normalizedSpan3.Slice(0, charsWritten3).ToString());

                    // c4 ==  NFC(c4) ==  NFC(c5)
                    AssertEqualsForm(c4, normalized4);
                    AssertEqualsForm(c4, normalized5);

                    AssertEqualsForm(c4, normalizedSpan4.Slice(0, charsWritten4).ToString());
                    AssertEqualsForm(c4, normalizedSpan5.Slice(0, charsWritten5).ToString());

                    // c2 is normalized to Form C
                    Assert.True(c2.IsNormalized(normForm), $"'{c2}' is marked as not normalized with form {normForm}");
                    Assert.True(c2.AsSpan().IsNormalized(normForm), $"'{c2}' span is marked as not normalized with form {normForm}");

                    // c4 is normalized to Form C
                    Assert.True(c4.IsNormalized(normForm), $"'{c4}' is marked as not normalized with form {normForm}");
                    Assert.True(c4.AsSpan().IsNormalized(normForm), $"'{c4}' span is marked as not normalized with form {normForm}");
                    break;

                case NormalizationForm.FormD:
                    // c3 ==  NFD(c1) ==  NFD(c2) ==  NFD(c3)
                    AssertEqualsForm(c3, normalized1);
                    AssertEqualsForm(c3, normalized2);
                    AssertEqualsForm(c3, normalized3);

                    AssertEqualsForm(c3, normalizedSpan1.Slice(0, charsWritten1).ToString());
                    AssertEqualsForm(c3, normalizedSpan2.Slice(0, charsWritten2).ToString());
                    AssertEqualsForm(c3, normalizedSpan3.Slice(0, charsWritten3).ToString());

                    // c5 ==  NFD(c4) ==  NFD(c5)
                    AssertEqualsForm(c5, normalized4);
                    AssertEqualsForm(c5, normalized5);

                    AssertEqualsForm(c5, normalizedSpan4.Slice(0, charsWritten4).ToString());
                    AssertEqualsForm(c5, normalizedSpan5.Slice(0, charsWritten5).ToString());

                    // c3 is normalized to Form D
                    Assert.True(c3.IsNormalized(normForm), $"'{c3}' is marked as not normalized with form {normForm}");
                    Assert.True(c3.AsSpan().IsNormalized(normForm), $"'{c3}' span is marked as not normalized with form {normForm}");

                    // c5 is normalized to Form D
                    Assert.True(c5.IsNormalized(normForm), $"'{c5}' is marked as not normalized with form {normForm}");
                    Assert.True(c5.AsSpan().IsNormalized(normForm), $"'{c5}' span is marked as not normalized with form {normForm}");
                    break;

                case NormalizationForm.FormKC:
                    // c4 == NFKC(c1) == NFKC(c2) == NFKC(c3) == NFKC(c4)
                    //    == NFKC(c5)
                    AssertEqualsForm(c4, normalized1);
                    AssertEqualsForm(c4, normalized2);
                    AssertEqualsForm(c4, normalized3);
                    AssertEqualsForm(c4, normalized4);
                    AssertEqualsForm(c4, normalized5);

                    AssertEqualsForm(c4, normalizedSpan1.Slice(0, charsWritten1).ToString());
                    AssertEqualsForm(c4, normalizedSpan2.Slice(0, charsWritten2).ToString());
                    AssertEqualsForm(c4, normalizedSpan3.Slice(0, charsWritten3).ToString());
                    AssertEqualsForm(c4, normalizedSpan4.Slice(0, charsWritten4).ToString());
                    AssertEqualsForm(c4, normalizedSpan5.Slice(0, charsWritten5).ToString());

                    // c4 is normalized to Form KC
                    Assert.True(c4.IsNormalized(normForm), $"'{c4}' is marked as not normalized with form {normForm}");
                    Assert.True(c4.AsSpan().IsNormalized(normForm), $"'{c4}' span is marked as not normalized with form {normForm}");
                    break;

                case NormalizationForm.FormKD:
                    // c5 == NFKD(c1) == NFKD(c2) == NFKD(c3) == NFKD(c4)
                    //    == NFKD(c5)
                    AssertEqualsForm(c5, normalized1);
                    AssertEqualsForm(c5, normalized2);
                    AssertEqualsForm(c5, normalized3);
                    AssertEqualsForm(c5, normalized4);
                    AssertEqualsForm(c5, normalized5);

                    AssertEqualsForm(c5, normalizedSpan1.Slice(0, charsWritten1).ToString());
                    AssertEqualsForm(c5, normalizedSpan2.Slice(0, charsWritten2).ToString());
                    AssertEqualsForm(c5, normalizedSpan3.Slice(0, charsWritten3).ToString());
                    AssertEqualsForm(c5, normalizedSpan4.Slice(0, charsWritten4).ToString());
                    AssertEqualsForm(c5, normalizedSpan5.Slice(0, charsWritten5).ToString());

                    // c5 is normalized to Form KD
                    Assert.True(c5.IsNormalized(normForm), $"'{c5}' is marked as not normalized with form {normForm}");
                    Assert.True(c5.AsSpan().IsNormalized(normForm), $"'{c5}' span is marked as not normalized with form {normForm}");
                    break;
            }
        }

        private static void AssertEqualsForm(string c, string cForm)
        {
            Assert.True(c.Equals(cForm), $"'{DumpStringAsCodepoints(c)}' is not matched with the normalized form '{DumpStringAsCodepoints(cForm)} with {cForm} normalization");
        }

        private static string DumpStringAsCodepoints(string s)
        {
            StringBuilder sb = new StringBuilder();
            for (int i=0; i<s.Length; i++)
            {
                sb.Append($"\\x{(int)s[i]:X4}");
            }
            return sb.ToString();
        }

    }
}
