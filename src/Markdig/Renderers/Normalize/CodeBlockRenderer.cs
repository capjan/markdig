// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license. 
// See the license.txt file in the project root for more information.

using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace Markdig.Renderers.Normalize
{
    /// <summary>
    /// An Normalize renderer for a <see cref="CodeBlock"/> and <see cref="FencedCodeBlock"/>.
    /// </summary>
    /// <seealso cref="Markdig.Renderers.Normalize.NormalizeObjectRenderer{Markdig.Syntax.CodeBlock}" />
    public class CodeBlockRenderer : NormalizeObjectRenderer<CodeBlock>
    {
        public bool OutputAttributesOnPre { get; set; }

        protected override void Write(NormalizeRenderer renderer, CodeBlock obj)
        {
            var fencedCodeBlock = obj as FencedCodeBlock;
            if (fencedCodeBlock != null)
            {
                var opening = new string(fencedCodeBlock.FencedChar, fencedCodeBlock.FencedCharCount);
                renderer.Write(opening);
                if (fencedCodeBlock.Info != null)
                {
                    renderer.Write(fencedCodeBlock.Info);
                }
                if (fencedCodeBlock.Arguments != null)
                {
                    renderer.Write(" ").Write(fencedCodeBlock.Arguments);
                }

                var attributes = obj.TryGetAttributes();
                if (attributes != null)
                {
                    renderer.Write(" ");
                    renderer.Write(attributes);
                }
                renderer.WriteLine();

                renderer.WriteLeafRawLines(obj, true, false);
                renderer.WriteLine(opening);
            }
            else
            {
                renderer.WriteLeafRawLines(obj, true, false, true);
                renderer.WriteLine();
            }
        }
    }
}