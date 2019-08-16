using System;
using System.Collections.Generic;
using System.Linq;
using SudokuServer.Models;

namespace SudokuServer.Services
{
    public class SudokuSolver
    {
        /// <summary>
        ///     Valid numbers to get random numbers for the cells.
        /// </summary>
        private readonly int[] _numbers = {1, 2, 3, 4, 5, 6, 7, 8, 9};

        /// <summary>
        ///     Random object to use creating random numbers.
        /// </summary>
        private readonly Random _random = new Random();

        /// <summary>
        ///     Sudoku board instance.
        /// </summary>
        private readonly SudokuBoard _sudokuBoard;

        /// <summary>
        ///     Cell indexes that excludes from backtracking.
        /// </summary>
        private readonly List<int> _theIndexesOfFilledCells = new List<int>();

        /// <summary>
        ///     The list to use for backtracking while solving the processes. Each list of specified index represents the blacklist
        ///     of the cell.
        /// </summary>
        private List<List<int>> _blackListsOfCells;

        /// <summary>
        ///     Creates a solver object for the specified Sudoku object.
        /// </summary>
        /// <param name="sudokuBoard">The sudokuBoard game object to use.</param>
        public SudokuSolver(SudokuBoard sudokuBoard)
        {
            _sudokuBoard = sudokuBoard;

            InitializeBlackList();
        }

        /// <summary>
        ///     Initialize the blacklist.
        /// </summary>
        private void InitializeBlackList()
        {
            _blackListsOfCells = new List<List<int>>(_sudokuBoard.TOTAL_CELLS);
            for (var index = 0; index < _blackListsOfCells.Capacity; index++) _blackListsOfCells.Add(new List<int>());
        }

        /// <summary>
        ///     Creates solved state to the game board and returns whether the puzzle solved.
        /// </summary>
        /// <param name="useRandomGenerator">Set it to true to see a different result for each solution.</param>
        /// <returns>Returns whether the board solved.</returns>
        public bool SolveThePuzzle(bool useRandomGenerator = true)
        {
            // Return false if the current state is board is not valid.
            if (!CheckTableStateIsValid()) return false;

            // Init protected index list to protect the current state of the board while backtracking.
            InitIndexListOfTheAlreadyFilledCells();

            // Clear the blacklist
            ClearBlackList();

            var currentCellIndex = 0;

            // Iterate all the cells of the board.
            while (currentCellIndex < _sudokuBoard.TOTAL_CELLS)
            {
                // If the current cell index is protected(which means it was inner the current state of the board), pass it.
                if (_theIndexesOfFilledCells.Contains(currentCellIndex))
                {
                    ++currentCellIndex;
                    continue;
                }

                // Clear blacklists of the indexes after the current index.
                ClearBlackList(currentCellIndex + 1);

                var currentCell = _sudokuBoard.GetCell(currentCellIndex);

                var theFoundValidNumber = GetValidNumberForTheCell(currentCellIndex, useRandomGenerator);

                // No valid number found for the cell. Let's backtrack.
                if (theFoundValidNumber == 0)
                {
                    // Let's backtrack
                    currentCellIndex = BacktrackTo(currentCellIndex);
                }
                else
                {
                    // Set found valid value to current cell.
                    _sudokuBoard.SetCellValue(theFoundValidNumber, currentCell.Index);
                    ++currentCellIndex;
                }
            }

            return true;
        }

        /// <summary>
        ///     Check current state of the table is valid.
        /// </summary>
        /// <returns>Returns whether is table is valid or not.</returns>
        public bool CheckTableStateIsValid(bool ignoreEmptyCells = false)
        {
            return _sudokuBoard.Cells
                       .Where(cell => !ignoreEmptyCells || cell.Value != -1)
                       .FirstOrDefault(cell => cell.Value != -1 && !IsValidValueForTheCell(cell.Value, cell)) == null;
        }

        /// <summary>
        ///     Checks the specified cell can accept the specified value.
        /// </summary>
        public bool IsValidValueForTheCell(int val, Cell cell)
        {
            // Check the value whether exists in the 3x3 group.
            if (_sudokuBoard.Cells.Where(c => c.Index != cell.Index && c.GroupNo == cell.GroupNo)
                    .FirstOrDefault(c2 => c2.Value == val) != null)
                return false;

            // Check the value whether exists in the row.
            if (_sudokuBoard.Cells.Where(c => c.Index != cell.Index && c.Position.Row == cell.Position.Row)
                    .FirstOrDefault(c2 => c2.Value == val) != null)
                return false;

            // Check the value whether exists in the column.
            if (_sudokuBoard.Cells.Where(c => c.Index != cell.Index && c.Position.Column == cell.Position.Column)
                    .FirstOrDefault(c2 => c2.Value == val) != null)
                return false;

            return true;
        }

        /// <summary>
        ///     Init protected index list to protect the current state of the board while backtracking.
        /// </summary>
        public void InitIndexListOfTheAlreadyFilledCells()
        {
            _theIndexesOfFilledCells.Clear();
            _theIndexesOfFilledCells.AddRange(_sudokuBoard.Cells
                .FindAll(cell => cell.Value != -1)
                .Select(cell => cell.Index));
        }

        /// <summary>
        ///     Backtracking operation for the cell specified with index.
        /// </summary>
        private int BacktrackTo(int index)
        {
            // Pass over the protected cells.
            while (_theIndexesOfFilledCells.Contains(--index)) ;

            // Get the back-tracked Cell.
            var backTrackedCell = _sudokuBoard.GetCell(index);

            // Add the value to the black-list of the back-tracked cell.
            AddToBlacklist(backTrackedCell.Value, index);

            // Reset the back-tracked cell value.
            backTrackedCell.Value = -1;

            // Reset the blacklist starting from the next one of the current tracking cell.
            ClearBlackList(index + 1);

            return index;
        }

        /// <summary>
        ///     Returns a valid number for the specified cell index.
        /// </summary>
        private int GetValidNumberForTheCell(int cellIndex, bool useRandomFactor)
        {
            var theFoundValidNumber = 0;

            // Find valid numbers for the cell.
            var validNumbers = _numbers.Where(x => !_blackListsOfCells[cellIndex].Contains(x)).ToArray();

            if (validNumbers.Length > 0)
            {
                // Return a (random) valid number from the valid numbers.
                var chosenIndex = useRandomFactor ? _random.Next(validNumbers.Length) : 0;
                theFoundValidNumber = validNumbers[chosenIndex];
            }

            // Try to get valid (random) value for the current cell, if no any valid value break the loop.
            do
            {
                var currentCell = _sudokuBoard.GetCell(cellIndex);

                // Check the found number if valid for the cell, if is not valid number for the cell then add the value to the blacklist of the cell.
                if (theFoundValidNumber != 0 && !IsValidValueForTheCell(theFoundValidNumber, currentCell))
                    AddToBlacklist(theFoundValidNumber, cellIndex);
                else
                    break;

                // Get a valid (random) value from valid numbers.
                theFoundValidNumber = GetValidNumberForTheCell(cellIndex, useRandomFactor);
            } while (theFoundValidNumber != 0);

            return theFoundValidNumber;
        }

        /// <summary>
        ///     Add given value into the specified index of the blacklist.
        /// </summary>
        private void AddToBlacklist(int value, int cellIndex)
        {
            _blackListsOfCells[cellIndex].Add(value);
        }

        /// <summary>
        ///     Initializes the black lists of the cells.
        /// </summary>
        /// <param name="startCleaningFromThisIndex">Clear the rest of the blacklist starting from the index.</param>
        private void ClearBlackList(int startCleaningFromThisIndex = 0)
        {
            for (var index = startCleaningFromThisIndex; index < _blackListsOfCells.Count; index++)
                _blackListsOfCells[index].Clear();
        }
    }
}