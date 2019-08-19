using System;
using System.Collections.Generic;
using System.Linq;
using SudokuServer.Dtos;
using SudokuServer.Models;

namespace SudokuServer.Services
{
    public class SudokuGameProcessor
    {
        private readonly SudokuBoard _sudokuBoard;
        private readonly SudokuSolver _sudokuSolver;
        private readonly object _lock = new object();

        public SudokuGameProcessor(SudokuBoard sudokuBoard, SudokuSolver sudokuSolver)
        {
            _sudokuBoard = sudokuBoard;
            _sudokuSolver = sudokuSolver;
        }

        public IEnumerable<CellDto> FilledCells
        {
            get
            {
                return _sudokuBoard.Cells
                    .Select(c => new CellDto {Value = c.Value, Row = c.Position.Row, Column = c.Position.Column})
                    .ToArray();
            }
        }

        public event EventHandler<CellDto> CellUpdated;
        public event EventHandler<Participant> GameCompleted;

        public void GenerateRandomPuzzle()
        {
            lock (_lock)
            {
                _sudokuBoard.Clear();
                _sudokuSolver.SolveThePuzzle(true);
                var random = new Random();
                var cellGroups = _sudokuBoard.Cells.GroupBy(c => c.GroupNo);

                foreach (var cellGroup in cellGroups)
                {
                    var cells = cellGroup.ToArray();
                    var total = cells.Count();
                    var upper = random.Next(total / 3, total / 3 * 2);
                    var available = new HashSet<int>();

                    for (var i = 0; i < upper; i++)
                    {
                        int inx;
                        do
                        {
                            inx = random.Next(0, total - 1);
                        } while (available.Contains(inx));

                        available.Add(inx);
                        cells[inx].Value = -1;
                    }
                }
            }
        }

//        public void GenerateRandomPuzzle()
//        {
//            lock (_lock)
//            {
//                _sudokuBoard.Clear();
//                _sudokuSolver.SolveThePuzzle(true);
//
//                _sudokuBoard.Cells.First().Value = -1;
//            }
//        }

        public bool SetCellValue(CellDto cellDto, Participant participant)
        {
            lock (_lock)
            {
                if (cellDto == null) return false;

                var cell = _sudokuBoard.Cells.First(c =>
                    c.Position.Column == cellDto.Column && c.Position.Row == cellDto.Row);

                if (!_sudokuSolver.IsValidValueForTheCell(cellDto.Value, cell)) return false;

                cell.Value = cellDto.Value;
                OnCellUpdated(cellDto);
                if (_sudokuBoard.IsBoardFilled())
                    OnGameCompleted(participant);
                return true;
            }
        }

        protected virtual void OnCellUpdated(CellDto e)
        {
            CellUpdated?.Invoke(this, e);
        }

        public bool IsBoardFilled()
        {
            return _sudokuBoard.IsBoardFilled();
        }

        protected virtual void OnGameCompleted(Participant e)
        {
            GenerateRandomPuzzle();
            GameCompleted?.Invoke(this, e);
        }
    }
}