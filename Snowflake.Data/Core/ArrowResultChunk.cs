/*
 * Copyright (c) 2012-2023 Snowflake Computing Inc. All rights reserved.
 */

using System;
using System.Collections.Generic;
using Apache.Arrow;
using Apache.Arrow.Types;
using Snowflake.Data.Log;

namespace Snowflake.Data.Core
{
    internal class ArrowResultChunk : BaseResultChunk
    {
        private static readonly SFLogger s_logger = SFLoggerFactory.GetLogger<ArrowResultChunk>();

        internal override ResultFormat ResultFormat => ResultFormat.ARROW;

        private static readonly DateTimeOffset s_epochDate = SFDataConverter.UnixEpoch;
        
        private static readonly long[] s_powersOf10 =  { 
            1, 
            10, 
            100, 
            1000, 
            10000, 
            100000, 
            1000000, 
            10000000, 
            100000000,
            1000000000
        };

        private const long TicksPerDay = (long)24 * 60 * 60 * 1000 * 10000;

        public List<RecordBatch> RecordBatch { get; set; }

        private sbyte[][] _sbyte;
        private short[][] _short;
        private int[][] _int;
        private long[][] _long;

        private byte[][] _byte;
        private double[][] _double;
        

        private int _currentBatchIndex = 0;
        private int _currentRecordIndex = -1;

        private void ResetTempTables()
        {
            _sbyte = new sbyte[ColumnCount][];
            _short = new short[ColumnCount][];
            _int = new int[ColumnCount][];
            _long = new long[ColumnCount][];
            _byte = new byte[ColumnCount][];
            _double = new double[ColumnCount][];
        }

        public ArrowResultChunk(RecordBatch recordBatch)
        {
            RecordBatch = new List<RecordBatch> { recordBatch };

            RowCount = recordBatch.Length;
            ColumnCount = recordBatch.ColumnCount;
            ChunkIndex = -1;
            
            ResetTempTables();
        }

        public ArrowResultChunk(int columnCount)
        {
            RecordBatch = new List<RecordBatch>();

            RowCount = 0;
            ColumnCount = columnCount;
            ChunkIndex = -1;

            ResetTempTables();
        }

        public void AddRecordBatch(RecordBatch recordBatch)
        {
            RecordBatch.Add(recordBatch);
        }
        
        internal override void Reset(ExecResponseChunk chunkInfo, int chunkIndex)
        {
            base.Reset(chunkInfo, chunkIndex);
            
            _currentBatchIndex = 0;
            _currentRecordIndex = -1;
            RecordBatch.Clear();

            ResetTempTables();
        }

        internal override bool Next()
        {
            if (_currentBatchIndex >= RecordBatch.Count)
                return false;
            
            _currentRecordIndex += 1;
            if (_currentRecordIndex < RecordBatch[_currentBatchIndex].Length)
                return true;

            _currentBatchIndex += 1;
            _currentRecordIndex = 0;

            ResetTempTables();

            return _currentBatchIndex < RecordBatch.Count;
        }

        internal override bool Rewind()
        {
            if (_currentRecordIndex == -1)
                return false;

            _currentRecordIndex -= 1;
            if (_currentRecordIndex >= 0)
                return true;

            _currentBatchIndex -= 1;

            if (_currentBatchIndex >= 0)
            {
                _currentRecordIndex = RecordBatch[_currentBatchIndex].Length - 1;

                ResetTempTables();
                return true;
            }

            return false;
        }

        [Obsolete("ExtractCell with rowIndex is deprecated", false)]
        public override UTF8Buffer ExtractCell(int rowIndex, int columnIndex)
        {
            throw new NotSupportedException();
        }

        public override UTF8Buffer ExtractCell(int columnIndex)
        {
            throw new NotSupportedException();
        }

        public string GetMetadata()
        {
            if (RecordBatch.Count == 0)
                return $"No data";

            return $"batches: {RecordBatch.Count}";
        }

        public object ExtractCell(int columnIndex, SFDataType srcType, long scale)
        {
            var column = RecordBatch[_currentBatchIndex].Column(columnIndex);

            if (column.IsNull(_currentRecordIndex))
                return DBNull.Value;
            
            switch (srcType)
            {
                case SFDataType.FIXED:
                    // Snowflake data types that are fixed-point numbers will fall into this category
                    // e.g. NUMBER, DECIMAL/NUMERIC, INT/INTEGER
                    switch (column.Data.DataType.TypeId)
                    {
                        case ArrowTypeId.Int8:
                            if (_sbyte[columnIndex] == null)
                                _sbyte[columnIndex] = ((Int8Array)column).Values.ToArray();
                            if (scale == 0)
                                return _sbyte[columnIndex][_currentRecordIndex];
                            else
                                return _sbyte[columnIndex][_currentRecordIndex] / (decimal)s_powersOf10[scale];

                        case ArrowTypeId.Int16:
                            if (_short[columnIndex] == null)
                                _short[columnIndex] = ((Int16Array)column).Values.ToArray();
                            if (scale == 0)
                                return _short[columnIndex][_currentRecordIndex];
                            else
                                return _short[columnIndex][_currentRecordIndex] / (decimal)s_powersOf10[scale];
                        
                        case ArrowTypeId.Int32:
                            if (_int[columnIndex] == null)
                                _int[columnIndex] = ((Int32Array)column).Values.ToArray();
                            if (scale == 0)
                                return _int[columnIndex][_currentRecordIndex];
                            else
                                return _int[columnIndex][_currentRecordIndex] / (decimal)s_powersOf10[scale];

                        case ArrowTypeId.Int64:
                            if (_long[columnIndex] == null)
                                _long[columnIndex] = ((Int64Array)column).Values.ToArray();
                            if (scale == 0)
                                return _long[columnIndex][_currentRecordIndex];
                            else
                                return _long[columnIndex][_currentRecordIndex] / (decimal)s_powersOf10[scale];
                        
                        case ArrowTypeId.Decimal128:
                            return ((Decimal128Array)column).GetValue(_currentRecordIndex);
                    }
                    break;

                case SFDataType.BOOLEAN:
                    if (_byte[columnIndex] == null)
                        _byte[columnIndex] = ((BooleanArray)column).Values.ToArray();
                    return _byte[columnIndex][_currentRecordIndex] == 1;

                case SFDataType.REAL:
                    // Snowflake data types that are floating-point numbers will fall in this category
                    // e.g. FLOAT/REAL/DOUBLE
                    if (_double[columnIndex] == null)
                        _double[columnIndex] = ((DoubleArray)column).Values.ToArray();
                    return _double[columnIndex][_currentRecordIndex];

                case SFDataType.TEXT:
                case SFDataType.ARRAY:
                case SFDataType.VARIANT:
                case SFDataType.OBJECT:
                    if (_byte[columnIndex] == null || _int[columnIndex] == null)
                    {
                        _byte[columnIndex] = ((StringArray)column).Values.ToArray();
                        _int[columnIndex] = ((StringArray)column).ValueOffsets.ToArray();
                    }
                    return StringArray.DefaultEncoding.GetString(
                        _byte[columnIndex], 
                        _int[columnIndex][_currentRecordIndex], 
                        _int[columnIndex][_currentRecordIndex + 1] - _int[columnIndex][_currentRecordIndex]);
                    
                case SFDataType.BINARY:
                    return ((BinaryArray)column).GetBytes(_currentRecordIndex).ToArray();
                
                case SFDataType.DATE:
                    if (_int[columnIndex] == null)
                        _int[columnIndex] = ((Date32Array)column).Values.ToArray();
                    return SFDataConverter.UnixEpoch.AddTicks(_int[columnIndex][_currentRecordIndex] * TicksPerDay);
                
                case SFDataType.TIME:
                {
                    var value = column.Data.DataType.TypeId == ArrowTypeId.Int32
                        ? ((Int32Array)column).GetValue(_currentRecordIndex)
                        : ((Int64Array)column).GetValue(_currentRecordIndex);
                    if (scale == 0)
                        return DateTimeOffset.FromUnixTimeSeconds(value.Value).DateTime;
                    if (scale <= 3)
                        return DateTimeOffset.FromUnixTimeMilliseconds(value.Value * s_powersOf10[3 - scale])
                            .DateTime;
                    if (scale <= 7)
                        return s_epochDate.AddTicks(value.Value * s_powersOf10[7 - scale]).DateTime;
                    return s_epochDate.AddTicks(value.Value / s_powersOf10[scale - 7]).DateTime;
                }
                case SFDataType.TIMESTAMP_TZ:
                    if (((StructArray)column).Fields.Count == 2)
                    {
                        var value = ((Int64Array)((StructArray)column).Fields[0]).GetValue(_currentRecordIndex);
                        var timezone = ((Int32Array)((StructArray)column).Fields[1]).GetValue(_currentRecordIndex);
                        var epoch = ExtractEpoch(value.Value, scale);
                        var fraction = ExtractFraction(value.Value, scale);
                        return s_epochDate.AddSeconds(epoch).AddTicks(fraction / 100).ToOffset(TimeSpan.FromMinutes(timezone.Value - 1440));
                    }
                    else
                    {
                        var epoch = ((Int64Array)((StructArray)column).Fields[0]).GetValue(_currentRecordIndex);
                        var fraction = ((Int32Array)((StructArray)column).Fields[1]).GetValue(_currentRecordIndex);
                        var timezone = ((Int32Array)((StructArray)column).Fields[2]).GetValue(_currentRecordIndex);
                        return s_epochDate.AddSeconds(epoch.Value).AddTicks(fraction.Value / 100).ToOffset(TimeSpan.FromMinutes(timezone.Value - 1440));
                    }

                case SFDataType.TIMESTAMP_LTZ:
                    if (column.Data.DataType.TypeId == ArrowTypeId.Struct)
                    {
                        var epoch = ((Int64Array)((StructArray)column).Fields[0]).GetValue(_currentRecordIndex);
                        var fraction = ((Int32Array)((StructArray)column).Fields[1]).GetValue(_currentRecordIndex);
                        return s_epochDate.AddSeconds(epoch.Value).AddTicks(fraction.Value / 100).ToLocalTime();
                    }
                    else
                    {
                        var value = ((Int64Array)column).GetValue(_currentRecordIndex);
                        var epoch = ExtractEpoch(value.Value, scale);
                        var fraction = ExtractFraction(value.Value, scale);
                        return s_epochDate.AddSeconds(epoch).AddTicks(fraction / 100).ToLocalTime();
                    }

                case SFDataType.TIMESTAMP_NTZ:
                    if (column.Data.DataType.TypeId == ArrowTypeId.Struct)
                    {
                        var epoch = ((Int64Array)((StructArray)column).Fields[0]).GetValue(_currentRecordIndex);
                        var fraction = ((Int32Array)((StructArray)column).Fields[1]).GetValue(_currentRecordIndex);
                        return s_epochDate.AddSeconds(epoch.Value).AddTicks(fraction.Value / 100).DateTime;
                    }
                    else
                    {
                        var value = ((Int64Array)column).GetValue(_currentRecordIndex);
                        var epoch = ExtractEpoch(value.Value, scale);
                        var fraction = ExtractFraction(value.Value, scale);
                        return s_epochDate.AddSeconds(epoch).AddTicks(fraction / 100).DateTime;
                    }
            }
            throw new NotSupportedException($"Type {srcType} is not supported.");
        }
        
        private long ExtractEpoch(long value, long scale)
        {
            return value / s_powersOf10[scale];
        }

        private long ExtractFraction(long value, long scale)
        {
            return ((value % s_powersOf10[scale]) * s_powersOf10[9 - scale]);
        }
    }
}
