/*
 * Apache License, Version 2.0
 * Copyright 2019-2020 NVIDIA Corporation
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Collections.Generic;
using System.IO;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Common.Test
{
    public class ExtensionMethodsTest
    {
        [RetryFact(DisplayName = "IsNull shall return false for empty input")]
        public void IsNull_WithEmptyInput()
        {
            List<string> list = new List<string>();
            Assert.False(list.IsNull());
        }

        [RetryFact(DisplayName = "IsNull shall return true for null input")]
        public void IsNull_WithNullInput()
        {
            List<string> list = null;
            Assert.True(list.IsNull());
        }

        [RetryFact(DisplayName = "IsNullOrEmpty shall return true for null input")]
        public void IsNullOrEmpty_WithNullInput()
        {
            List<string> list = null;
            Assert.True(list.IsNullOrEmpty());
        }

        [RetryFact(DisplayName = "IsNullOrEmpty shall return true for empty list")]
        public void IsNullOrEmpty_WithEmptyList()
        {
            List<string> list = new List<string>();
            Assert.True(list.IsNullOrEmpty());
        }

        [RetryFact(DisplayName = "IsNullOrEmpty to use Any with non ICollection")]
        public void IsNullOrEmpty_UseAnyForNonICollection()
        {
            var stack = new Stack<int>();
            stack.Push(1);
            Assert.False(stack.IsNullOrEmpty());
        }

        [Theory(DisplayName = "IsNullOrEmpty shall return false with at least one items")]
        [InlineData("go")]
        [InlineData("team", "clara")]
        [InlineData("team", "clara", "rocks")]
        public void IsNullOrEmpty_WithOneItemInList(params string[] items)
        {
            List<string> list = new List<string>(items);
            Assert.False(ExtensionMethods.IsNullOrEmpty(list));
        }

        [Theory(DisplayName = "RemoveInvalidChars shall return original input")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void RemoveInvalidChars_HandleNullInput(string input)
        {
            Assert.Equal(input, ExtensionMethods.RemoveInvalidPathChars(input));
        }

        [RetryFact(DisplayName = "RemoveInvalidChars shall remove invalid path characters")]
        public void RemoveInvalidChars_ShallRemoveInvalidPathCharacters()
        {
            var invalidChars = string.Join("", Path.GetInvalidPathChars());
            var input = "team" + invalidChars + "clara";

            Assert.Equal("teamclara", input.RemoveInvalidPathChars());
        }

        [RetryFact(DisplayName = "FixJobName shall remove invalid characters")]
        public void FixJobName_ShellRemoveInvalidChars()
        {
            var invalidChars = string.Join("", Path.GetInvalidPathChars());
            var input = "_~!@#$%^&*()=+[]\\|:;\"',<.>/?";

            foreach (var c in input)
            {
                Assert.Equal("a-z", $"{c}A{c}{c}{c}Z{c}".FixJobName());
            }
        }

        [RetryFact(DisplayName = "FixJobName shall trim length if exceeds limit")]
        public void FixJobName_ShallTrimLength()
        {
            var invalidChars = string.Join("", Path.GetInvalidPathChars());
            var input = "ABCDEFGHIJKLMNOPQRSTUVWXYZ01234567890!!!";

            Assert.Equal("abcdefghijklmnopqrstuvwxy", input.FixJobName());
        }
    }
}