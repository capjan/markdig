﻿// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license. 
// See the license.txt file in the project root for more information.
using Markdig.Parsers;
using Markdig.Syntax;

namespace Markdig.Extensions.Tables
{
    public class GridTableParser : BlockParser
    {
        public GridTableParser()
        {
            OpeningCharacters = new[] {'+'};
        }

        public override BlockState TryOpen(BlockProcessor processor)
        {
            // A grid table cannot start more than an indent
            if (processor.IsCodeIndent)
            {
                return BlockState.None;
            }

            var line = processor.Line;

            // A grid table must start with a line like this:
            // + ------------- + ------------ + ---------------------------------------- +
            // Spaces are optional

            GridTableState tableState = null;
            var c = line.CurrentChar;
            var startPosition = processor.Start;
            while (true)
            {
                if (c == '+')
                {
                    var startCharacter = line.Start;
                    line.NextChar();
                    if (line.IsEmptyOrWhitespace())
                    {
                        if (tableState == null)
                        {
                            return BlockState.None;
                        }
                        break;
                    }

                    TableColumnAlign align;
                    if (TableHelper.ParseColumnHeader(ref line, '-', out align))
                    {
                        if (tableState == null)
                        {
                            tableState = new GridTableState()
                            {
                                Start = processor.Start,
                                ExpectRow = true,
                            };
                        }
                        tableState.AddColumn(startCharacter - startPosition, line.Start - 1 - startPosition, align);

                        c = line.CurrentChar;
                        continue;
                    }
                }

                // If we have any other characters, this is an invalid line
                return BlockState.None;
            }

            // Store the line (if we need later to build a ParagraphBlock because the GridTable was in fact invalid)
            tableState.AddLine(ref processor.Line);

            // Create the grid table
            var table = new Table(this);

            table.SetData(typeof(GridTableState), tableState);


            // Calculate the total width of all columns
            int totalWidth = 0;
            foreach (var columnSlice in tableState.ColumnSlices)
            {
                totalWidth += columnSlice.End - columnSlice.Start;
            }

            // Store the column width and alignment
            foreach (var columnSlice in tableState.ColumnSlices)
            {
                var columnDefinition = new TableColumnDefinition
                {
                    // Column width proportional to the total width
                    Width = (float)(columnSlice.End - columnSlice.Start) * 100.0f / totalWidth,
                    Alignment = columnSlice.Align,
                };
                table.ColumnDefinitions.Add(columnDefinition);
            }

            processor.NewBlocks.Push(table);

            return BlockState.ContinueDiscard;
        }

        public override BlockState TryContinue(BlockProcessor processor, Block block)
        {
            var gridTable = (Table) block;
            var tableState = (GridTableState)block.GetData(typeof(GridTableState));

            // We expect to start at the same 
            //if (processor.Start == tableState.Start)
            {
                var columns = tableState.ColumnSlices;

                foreach (var columnSlice in columns)
                {
                    columnSlice.PreviousColumnSpan = columnSlice.CurrentColumnSpan;
                    columnSlice.CurrentColumnSpan = 0;
                }

                if (processor.CurrentChar == '+')
                {
                    var result = ParseRowSeparator(processor, tableState, gridTable);
                    if (result != BlockState.None)
                    {
                        return result;
                    }
                }
                else if (processor.CurrentChar == '|')
                {
                    var line = processor.Line;

                    // | ------------- | ------------ | ---------------------------------------- |
                    // Calculate the colspan for the new row
                    int columnIndex = -1;
                    foreach (var columnSlice in columns)
                    {
                        if (line.PeekCharExtra(columnSlice.Start) == '|')
                        {
                            columnIndex++;
                        }
                        if (columnIndex >= 0)
                        {
                            columns[columnIndex].CurrentColumnSpan++;
                        }
                    }

                    // Check if the colspan of the current row is the same than the previous row
                    bool continueRow = true;
                    foreach (var columnSlice in columns)
                    {
                        if (columnSlice.PreviousColumnSpan != columnSlice.CurrentColumnSpan)
                        {
                            continueRow = false;
                            break;
                        }
                    }

                    // If the current row doesn't continue the previous row (col span are different)
                    // Close the previous row
                    if (!continueRow)
                    {
                        TerminateLastRow(processor, tableState, gridTable, false);
                    }

                    for (int i = 0; i < columns.Count;)
                    {
                        var column = columns[i];
                        var nextColumnIndex = i + column.CurrentColumnSpan;
                        // If the span is 0, we exit
                        if (nextColumnIndex == i)
                        {
                            break;
                        }
                        var nextColumn = nextColumnIndex < columns.Count ? columns[nextColumnIndex] : null;

                        var sliceForCell = line;
                        sliceForCell.Start = line.Start + column.Start + 1;
                        if (nextColumn != null)
                        {
                            sliceForCell.End = line.Start + nextColumn.Start - 1;
                        }
                        else
                        {
                            var columnEnd = columns[columns.Count - 1].End;
                            // If there is a `|` exactly at the expected end of the table row, we cut the line
                            // otherwise we allow to have the last cell of a row to be open for longer cell content
                            if (line.PeekCharExtra(columnEnd + 1) == '|')
                            {
                                sliceForCell.End = line.Start + columnEnd;
                            }
                        }
                        sliceForCell.TrimEnd();

                        // Process the content of the cell
                        column.BlockProcessor.LineIndex = processor.LineIndex;
                        column.BlockProcessor.ProcessLine(sliceForCell);

                        // Go to next column
                        i = nextColumnIndex;
                    }

                    return BlockState.ContinueDiscard;
                }
            }

            TerminateLastRow(processor, tableState, gridTable, true);

            // If we don't have a row, it means that only the header was valid
            // So we need to remove the grid table, and create a ParagraphBlock
            // with the 2 slices 
            if (gridTable.Count == 0)
            {
                var parser = processor.Parsers.FindExact<ParagraphBlockParser>();
                // Discard the grid table
                var parent = gridTable.Parent;
                processor.Discard(gridTable);
                var paragraphBlock = new ParagraphBlock(parser)
                {
                    Lines = tableState.Lines,
                };
                parent.Add(paragraphBlock);
                processor.Open(paragraphBlock);
            }

            return BlockState.Break;
        }

        public override bool Close(BlockProcessor processor, Block block)
        {
            // Work only on Table, not on TableCell
            var gridTable = block as Table;
            if (gridTable != null)
            {
                var tableState = (GridTableState) block.GetData(typeof (GridTableState));
                TerminateLastRow(processor, tableState, gridTable, true);
            }
            return true;
        }

        private BlockState ParseRowSeparator(BlockProcessor state, GridTableState tableState, Table gridTable)
        {
            // A grid table must start with a line like this:
            // + ------------- + ------------ + ---------------------------------------- +
            // Spaces are optional

            var line = state.Line;
            var c = line.CurrentChar;
            bool isFirst = true;
            var delimiterChar = '\0';
            while (true)
            {
                if (c == '+')
                {
                    line.NextChar();
                    if (line.IsEmptyOrWhitespace())
                    {
                        if (isFirst)
                        {
                            return BlockState.None;
                        }
                        break;
                    }

                    TableColumnAlign align;
                    if (TableHelper.ParseColumnHeaderDetect(ref line, ref delimiterChar, out align))
                    {
                        isFirst = false;
                        c = line.CurrentChar;
                        continue;
                    }
                }

                // If we have any other characters, this is an invalid line
                return BlockState.None;
            }

            // If we have an header row
            var isHeader = delimiterChar == '=';

            // Terminate the current row
            TerminateLastRow(state, tableState, gridTable, false);

            // If we had a header row separator, we can mark all rows since last row separator 
            // to be header rows
            if (isHeader)
            {
                for (int i = tableState.StartRowGroup; i < gridTable.Count; i++)
                {
                    var row = (TableRow) gridTable[i];
                    row.IsHeader = true;
                }
            }

            // Makr the next start row group continue on the next row
            tableState.StartRowGroup = gridTable.Count;

            // We don't keep the line
            return BlockState.ContinueDiscard;
        }

        private void TerminateLastRow(BlockProcessor state, GridTableState tableState, Table gridTable, bool isLastRow)
        {
            var columns = tableState.ColumnSlices;
            TableRow currentRow = null;
            foreach (var columnSlice in columns)
            {
                if (columnSlice.CurrentCell != null)
                {
                    if (currentRow == null)
                    {
                        currentRow = new TableRow();
                    }
                    currentRow.Add(columnSlice.CurrentCell);
                    columnSlice.BlockProcessor.Close(columnSlice.CurrentCell);
                }

                // Renew the block parser processor (or reset it for the last row)
                if (columnSlice.BlockProcessor != null)
                {
                    columnSlice.BlockProcessor.ReleaseChild();
                    columnSlice.BlockProcessor = isLastRow ? null : state.CreateChild();
                }

                // Create or erase the cell
                if (isLastRow || columnSlice.CurrentColumnSpan == 0)
                {
                    // We don't need the cell anymore if we have a last row
                    // Or the cell has a columnspan == 0
                    columnSlice.CurrentCell = null;
                }
                else
                {
                    // Else we can create a new cell
                    columnSlice.CurrentCell = new TableCell(this)
                    {
                        ColumnSpan = columnSlice.CurrentColumnSpan
                    };

                    if (columnSlice.BlockProcessor == null)
                    {
                        columnSlice.BlockProcessor = state.CreateChild();
                    }

                    // Ensure that the BlockParser is aware that the TableCell is the top-level container
                    columnSlice.BlockProcessor.Open(columnSlice.CurrentCell);
                }
            }

            if (currentRow != null)
            {
                gridTable.Add(currentRow);
            }
        }
    }
}