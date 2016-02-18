﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Textamina.Markdig.Helpers
{
    public class StringBuilderCache
    {
        /// <summary>
        /// A StringBuilder that can be used locally in a method body only.
        /// </summary>
        [ThreadStatic]
        public static readonly StringBuilder Local = new StringBuilder();

        private readonly Stack<StringBuilder> builders;

        public StringBuilderCache()
        {
            builders = new Stack<StringBuilder>();
        }

        public StringBuilder Get()
        {
            if (builders.Count > 0)
            {
                return builders.Pop();
            }

            return new StringBuilder();
        }

        public void Release(StringBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            builder.Clear();
            builders.Push(builder);
        }
    }
}