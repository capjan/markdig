// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license. 
// See the license.txt file in the project root for more information.

using System;
using System.Globalization;
using Markdig.Renderers;
using Markdig.Renderers.Html;

namespace Markdig.Extensions.Tables
{
    /// <summary>
    /// A HTML renderer for a <see cref="Table"/>
    /// </summary>
    /// <seealso cref="Markdig.Renderers.Html.HtmlObjectRenderer{Markdig.Extensions.Tables.TableBlock}" />
    public class HtmlTableRenderer : HtmlObjectRenderer<Table>
    {
        protected override void Write(HtmlRenderer renderer, Table table)
        {
            renderer.EnsureLine();
            renderer.Write("<table class=\"bxstyle\"").WriteAttributes(table).WriteLine(">");

            bool hasBody = false;
            bool hasAlreadyHeader = false;
            bool isHeaderOpen = false;


            bool hasColumnWidth = false;
            foreach (var tableColumnDefinition in table.ColumnDefinitions)
            {
                if (tableColumnDefinition.Width != 0.0f && tableColumnDefinition.Width != 1.0f)
                {
                    hasColumnWidth = true;
                    break;
                }
            }

            if (hasColumnWidth)
            {
                foreach (var tableColumnDefinition in table.ColumnDefinitions)
                {
                    var width = Math.Round(tableColumnDefinition.Width*100)/100;
                    var widthValue = string.Format(CultureInfo.InvariantCulture, "{0:n2}", width);
                    renderer.WriteLine($"<col style=\"width:{widthValue}%\">");
                }
            }

            foreach (var rowObj in table)
            {
                var row = (TableRow)rowObj;
                if (row.IsHeader)
                {
                    // Allow a single thead
                    if (!hasAlreadyHeader)
                    {
                        renderer.WriteLine("<thead>");
                        isHeaderOpen = true;
                    }
                    hasAlreadyHeader = true;
                }
                else if (!hasBody)
                {
                    if (isHeaderOpen)
                    {
                        renderer.WriteLine("</thead>");
                        isHeaderOpen = false;
                    }

                    renderer.WriteLine("<tbody>");
                    hasBody = true;
                }
                renderer.WriteLine("<tr>");
                for (int i = 0; i < row.Count; i++)
                {
                    var cellObj = row[i];
                    var cell = (TableCell)cellObj;

                    renderer.EnsureLine();
                    renderer.Write(row.IsHeader ? "<th" : "<td");
                    if (cell.ColumnSpan != 1)
                    {
                        renderer.Write($" colspan=\"{cell.ColumnSpan}\"");
                    }

                    if (table.ColumnDefinitions != null && i < table.ColumnDefinitions.Count)
                    {
                        switch (table.ColumnDefinitions[i].Alignment)
                        {
                            case TableColumnAlign.Center:
                                renderer.Write(" style=\"text-align: center;\"");
                                break;
                            case TableColumnAlign.Right:
                                renderer.Write(" style=\"text-align: right;\"");
                                break;
                        }
                    }
                    renderer.WriteAttributes(cell);
                    renderer.Write(">");

                    var previousImplicitParagraph = renderer.ImplicitParagraph;
                    if (cell.Count == 1)
                    {
                        renderer.ImplicitParagraph = true;
                    }
                    renderer.Write(cell);
                    renderer.ImplicitParagraph = previousImplicitParagraph;
                    
                    renderer.WriteLine(row.IsHeader ? "</th>" : "</td>");
                }
                renderer.WriteLine("</tr>");
            }

            if (hasBody)
            {
                renderer.WriteLine("</tbody>");
            }
            else if (isHeaderOpen)
            {
                renderer.WriteLine("</thead>");
            }
            renderer.WriteLine("</table>");
        }
    }
}