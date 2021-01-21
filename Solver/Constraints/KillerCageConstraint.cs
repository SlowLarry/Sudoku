﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using SudokuBlazor.Shared;
using static SudokuBlazor.Solver.SolverUtility;

namespace SudokuBlazor.Solver.Constraints
{
    public class KillerCageConstraint : Constraint
    {
        private readonly List<(int, int)> cells;
        private readonly int sum;
        private List<List<int>> sumCombinations = null;
        private HashSet<int> possibleValues = null;

        public KillerCageConstraint(List<(int, int)> cells, int sum)
        {
            this.cells = cells;
            this.sum = sum;
            InitCombinations();
        }

        public KillerCageConstraint(JObject jobject)
        {
            int version = (int)jobject["v"];
            if (version != 1)
            {
                return;
            }

            cells = new(DeserializeCells(jobject["cells"]));
            sum = (int)jobject["sum"];
            InitCombinations();
        }

        public override string Serialized => new JObject()
        {
            ["type"] = "KillerCage",
            ["v"] = 1,
            ["cells"] = SerializeCells(cells),
            ["sum"] = sum,
        }.ToString();

        public override string Name => "Killer Cage";

        public override string SpecificName => $"Killer Cage at {cells[0]}";

        public override string Icon => "";

        public override string Rules => "Digits in a cage must sum to the number at the top left of the cage. Digits may not repeat in a cage.";

        public override bool MarkConflicts(int[] values, bool[] conflicts)
        {
            if (cells.Any(cell => values[FlatIndex(cell)] == 0))
            {
                return false;
            }

            int cellSum = cells.Sum(cell => values[FlatIndex(cell)]);
            if (cellSum != sum)
            {
                foreach (var cell in cells)
                {
                    conflicts[FlatIndex(cell)] = true;
                }
                return true;
            }
            return false;
        }

        private void InitCombinations()
        {
            const int allValueSum = (MAX_VALUE * (MAX_VALUE + 1)) / 2;
            if (sum > 0 && sum < allValueSum)
            {
                sumCombinations = new();
                possibleValues = new();
                int numCells = cells.Count;
                foreach (var combination in Enumerable.Range(1, MAX_VALUE).Combinations(numCells))
                {
                    if (combination.Sum() == sum)
                    {
                        sumCombinations.Add(combination);
                        foreach (int value in combination)
                        {
                            possibleValues.Add(value);
                        }
                    }
                }
            }
        }

        public override LogicResult InitCandidates(SudokuSolver sudokuSolver)
        {
            LogicResult result = LogicResult.None;
            if (possibleValues != null && possibleValues.Count < MAX_VALUE)
            {
                var board = sudokuSolver.Board;
                for (int v = 1; v <= 9; v++)
                {
                    if (!possibleValues.Contains(v))
                    {
                        uint valueMask = ValueMask(v);
                        foreach (var cell in cells)
                        {
                            uint cellMask = board[cell.Item1, cell.Item2];
                            if ((cellMask & valueMask) != 0)
                            {
                                if (!sudokuSolver.ClearValue(cell.Item1, cell.Item2, v))
                                {
                                    return LogicResult.Invalid;
                                }
                                result = LogicResult.Changed;
                            }
                        }
                    }
                }
            }

            return result;
        }

        public override bool EnforceConstraint(SudokuSolver sudokuSolver, int i, int j, int val)
        {
            // Determine if the sum is now complete
            if (cells.Contains((i, j)) && cells.All(cell => sudokuSolver.IsValueSet(cell.Item1, cell.Item2)))
            {
                return cells.Select(cell => sudokuSolver.GetValue(cell)).Sum() == sum;
            }
            return true;
        }

        public override LogicResult StepLogic(SudokuSolver sudokuSolver, StringBuilder logicalStepDescription, bool isBruteForcing)
        {
            bool changed = false;
            if (sumCombinations == null || sumCombinations.Count == 0 || isBruteForcing)
            {
                return LogicResult.None;
            }

            // Reduce the remaining cell options
            int numUnset = 0;
            int setSum = 0;
            uint valueUsedMask = 0;
            uint valuePresentMask = 0;
            List<List<int>> validCombinations = sumCombinations.ToList();
            var board = sudokuSolver.Board;
            foreach (var curCell in cells)
            {
                uint cellMask = board[curCell.Item1, curCell.Item2];
                valuePresentMask |= (cellMask & ~valueSetMask);
                if (IsValueSet(cellMask))
                {
                    int curValue = GetValue(cellMask);
                    setSum += curValue;
                    validCombinations.RemoveAll(list => !list.Contains(curValue));
                    valueUsedMask |= (cellMask & ~valueSetMask);
                }
                else
                {
                    numUnset++;
                }
            }

            // Remove combinations which require a value which isn't present
            validCombinations.RemoveAll(list => list.Any(v => (valuePresentMask & ValueMask(v)) == 0));

            if (validCombinations.Count == 0)
            {
                // Sum is no longer possible
                logicalStepDescription.Append($"No more valid combinations which sum to {sum}.");
                return LogicResult.Invalid;
            }

            if (numUnset > 0)
            {
                uint valueRemainingMask = 0;
                foreach (var combination in validCombinations)
                {
                    foreach (int v in combination)
                    {
                        valueRemainingMask |= ValueMask(v);
                    }
                }
                valueRemainingMask &= ~valueUsedMask;

                var unsetCells = cells.Where(cell => !IsValueSet(board[cell.Item1, cell.Item2])).ToList();
                var unsetCombinations = validCombinations.Select(list => list.Where(v => (valueUsedMask & ValueMask(v)) == 0).ToList()).ToList();
                var unsetCellCurMasks = unsetCells.Select(cell => board[cell.Item1, cell.Item2]).ToList();
                uint[] unsetCellNewMasks = new uint[unsetCells.Count];
                foreach (var curCombination in unsetCombinations)
                {
                    foreach (var permutation in curCombination.Permuatations())
                    {
                        bool permutationValid = true;
                        for (int i = 0; i < permutation.Count; i++)
                        {
                            uint cellMask = unsetCellCurMasks[i];
                            uint permValueMask = ValueMask(permutation[i]);
                            if ((cellMask & permValueMask) == 0)
                            {
                                permutationValid = false;
                                break;
                            }
                        }

                        if (permutationValid)
                        {
                            for (int i = 0; i < permutation.Count; i++)
                            {
                                unsetCellNewMasks[i] |= ValueMask(permutation[i]);
                            }
                        }
                    }
                }

                for (int i = 0; i < unsetCells.Count; i++)
                {
                    var curCell = unsetCells[i];
                    uint cellMask = board[curCell.Item1, curCell.Item2];
                    uint newCellMask = unsetCellNewMasks[i];
                    if (newCellMask != cellMask)
                    {
                        changed = true;
                        if (!sudokuSolver.SetMask(curCell.Item1, curCell.Item2, newCellMask))
                        {
                            // Cell has no values remaining
                            logicalStepDescription.Append($"{CellName(curCell)} has no more remaining values.");
                            return LogicResult.Invalid;
                        }
                        logicalStepDescription.Append($"{CellName(curCell)} reduced to possibilities: {MaskToString(newCellMask)}");
                    }
                }
            }
            else
            {
                // Ensure the sum is correct
                if (setSum != sum)
                {
                    logicalStepDescription.Append($"Sums to {setSum} instead of {sum}.");
                    return LogicResult.Invalid;
                }
            }
            return changed ? LogicResult.Changed : LogicResult.None;
        }

        public override List<(int, int)> Group => cells;
    }
}
