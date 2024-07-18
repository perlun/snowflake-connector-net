using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Snowflake.Data.Client;
using Snowflake.Data.Core.Converter;
using Snowflake.Data.Tests.Client;

namespace Snowflake.Data.Tests.IntegrationTests
{
    [TestFixture]
    [IgnoreOnEnvIs("snowflake_cloud_env", new [] { "AZURE", "GCP" })]
    public class StructuredTypesIT : SFBaseTest
    {
        private static string _tableName = "structured_types_tests";

        [Test]
        public void TestInsertStructuredTypeObject()
        {
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                // arrange
                connection.Open();
                CreateOrReplaceTable(connection, _tableName, new List<string> { "address OBJECT(city VARCHAR, state VARCHAR)" });
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var addressAsSFString = "OBJECT_CONSTRUCT('city','San Mateo', 'state', 'CA')::OBJECT(city VARCHAR, state VARCHAR)";
                    command.CommandText = $"INSERT INTO {_tableName} SELECT {addressAsSFString}";
                    command.ExecuteNonQuery();
                    command.CommandText = $"SELECT * FROM {_tableName}";

                    // act
                    var reader = command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                }
            }
        }

        [Test]
        public void TestSelectStructuredTypeObject()
        {
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                // arrange
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var addressAsSFString = "OBJECT_CONSTRUCT('city','San Mateo', 'state', 'CA')::OBJECT(city VARCHAR, state VARCHAR)";
                    command.CommandText = $"SELECT {addressAsSFString}";

                    // act
                    var reader = (SnowflakeDbDataReader)command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var address = reader.GetObject<Address>(0);
                    Assert.AreEqual("San Mateo", address.city);
                    Assert.AreEqual("CA", address.state);
                    Assert.IsNull(address.zip);
                }
            }
        }

        [Test]
        [TestCase(StructureTypeConstructionMethod.PROPERTIES_NAMES)]
        [TestCase(StructureTypeConstructionMethod.PROPERTIES_ORDER)]
        [TestCase(StructureTypeConstructionMethod.CONSTRUCTOR)]
        public void TestSelectNestedStructuredTypeObject(StructureTypeConstructionMethod constructionMethod)
        {
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                // arrange
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var addressAsSFString =
                        "OBJECT_CONSTRUCT('city','San Mateo', 'state', 'CA', 'zip', OBJECT_CONSTRUCT('prefix', '00', 'postfix', '11'))::OBJECT(city VARCHAR, state VARCHAR, zip OBJECT(prefix VARCHAR, postfix VARCHAR))";
                    command.CommandText = $"SELECT {addressAsSFString}";

                    // act
                    var reader = (SnowflakeDbDataReader)command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var address = reader.GetObject<Address>(0, constructionMethod);
                    Assert.AreEqual("San Mateo", address.city);
                    Assert.AreEqual("CA", address.state);
                    Assert.NotNull(address.zip);
                    Assert.AreEqual("00", address.zip.prefix);
                    Assert.AreEqual("11", address.zip.postfix);
                }
            }
        }

        [Test]
        public void TestSelectObjectWithMap()
        {
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                // arrange
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var objectWithMap = "OBJECT_CONSTRUCT('names', OBJECT_CONSTRUCT('Excellent', '6', 'Poor', '1'))::OBJECT(names MAP(VARCHAR,VARCHAR))";
                    command.CommandText = $"SELECT {objectWithMap}";

                    // act
                    var reader = (SnowflakeDbDataReader)command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var grades = reader.GetObject<GradesWithMap>(0);
                    Assert.NotNull(grades);
                    Assert.AreEqual(2, grades.Names.Count);
                    Assert.AreEqual("6", grades.Names["Excellent"]);
                    Assert.AreEqual("1", grades.Names["Poor"]);
                }
            }
        }

        [Test]
        public void TestSelectArray()
        {
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                // arrange
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var arrayOfNumberSFString = "ARRAY_CONSTRUCT('a','b','c')::ARRAY(TEXT)";
                    command.CommandText = $"SELECT {arrayOfNumberSFString}";

                    // act
                    var reader = (SnowflakeDbDataReader)command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var array = reader.GetArray<string>(0);
                    Assert.AreEqual(3, array.Length);
                    CollectionAssert.AreEqual(new[] { "a", "b", "c" }, array);
                }
            }
        }

        [Test]
        public void TestSelectArrayOfObjects()
        {
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                // arrange
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var arrayOfObjects =
                        "ARRAY_CONSTRUCT(OBJECT_CONSTRUCT('name', 'Alex'), OBJECT_CONSTRUCT('name', 'Brian'))::ARRAY(OBJECT(name VARCHAR))";
                    command.CommandText = $"SELECT {arrayOfObjects}";

                    // act
                    var reader = (SnowflakeDbDataReader)command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var array = reader.GetArray<Identity>(0);
                    Assert.AreEqual(2, array.Length);
                    CollectionAssert.AreEqual(new[] { new Identity("Alex"), new Identity("Brian") }, array);
                }
            }
        }


        [Test]
        public void TestSelectArrayOfArrays()
        {
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                // arrange
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var arrayOfObjects = "ARRAY_CONSTRUCT(ARRAY_CONSTRUCT('a', 'b'), ARRAY_CONSTRUCT('c', 'd'))::ARRAY(ARRAY(TEXT))";
                    command.CommandText = $"SELECT {arrayOfObjects}";

                    // act
                    var reader = (SnowflakeDbDataReader)command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var array = reader.GetArray<string[]>(0);
                    Assert.AreEqual(2, array.Length);
                    CollectionAssert.AreEqual(new[] { new[] { "a", "b" }, new[] { "c", "d" } }, array);
                }
            }
        }

        [Test]
        public void TestSelectArrayOfMap()
        {
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                // arrange
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var arrayOfMap = "ARRAY_CONSTRUCT(OBJECT_CONSTRUCT('a', 'b'))::ARRAY(MAP(VARCHAR,VARCHAR))";
                    command.CommandText = $"SELECT {arrayOfMap}";

                    // act
                    var reader = (SnowflakeDbDataReader)command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var array = reader.GetArray<Dictionary<string, string>>(0);
                    Assert.AreEqual(1, array.Length);
                    var map = array[0];
                    Assert.NotNull(map);
                    Assert.AreEqual(1, map.Count);
                    Assert.AreEqual("b",map["a"]);
                }
            }
        }

        [Test]
        public void TestSelectObjectWithArrays()
        {
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                // arrange
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var objectWithArray = "OBJECT_CONSTRUCT('names', ARRAY_CONSTRUCT('Excellent', 'Poor'))::OBJECT(names ARRAY(TEXT))";
                    command.CommandText = $"SELECT {objectWithArray}";

                    // act
                    var reader = (SnowflakeDbDataReader)command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var grades = reader.GetObject<Grades>(0);
                    Assert.NotNull(grades);
                    CollectionAssert.AreEqual(new[] { "Excellent", "Poor" }, grades.Names);
                }
            }
        }

        [Test]
        public void TestSelectObjectWithList()
        {
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                // arrange
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var objectWithArray = "OBJECT_CONSTRUCT('names', ARRAY_CONSTRUCT('Excellent', 'Poor'))::OBJECT(names ARRAY(TEXT))";
                    command.CommandText = $"SELECT {objectWithArray}";

                    // act
                    var reader = (SnowflakeDbDataReader)command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var grades = reader.GetObject<GradesWithList>(0);
                    Assert.NotNull(grades);
                    CollectionAssert.AreEqual(new List<string> { "Excellent", "Poor" }, grades.Names);
                }
            }
        }

        [Test]
        public void TestSelectMap()
        {
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                // arrange
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var addressAsSFString = "OBJECT_CONSTRUCT('city','San Mateo', 'state', 'CA', 'zip', '01-234')::MAP(VARCHAR, VARCHAR)";
                    // var addressAsSFString = "{'city': 'San Mateo', 'state': 'CA'}::MAP(VARCHAR, VARCHAR)";
                    command.CommandText = $"SELECT {addressAsSFString}";

                    // act
                    var reader = (SnowflakeDbDataReader)command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var map = reader.GetMap<string, string>(0);
                    Assert.AreEqual(3, map.Count);
                    Assert.AreEqual("San Mateo", map["city"]);
                    Assert.AreEqual("CA", map["state"]);
                    Assert.AreEqual("01-234", map["zip"]);
                }
            }
        }

        [Test]
        public void TestSelectMapWithIntegerKeys()
        {
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                // arrange
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var mapSfString = "OBJECT_CONSTRUCT('5','San Mateo', '8', 'CA', '13', '01-234')::MAP(INTEGER, VARCHAR)";
                    command.CommandText = $"SELECT {mapSfString}";

                    // act
                    var reader = (SnowflakeDbDataReader)command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var map = reader.GetMap<int, string>(0);
                    Assert.AreEqual(3, map.Count);
                    Assert.AreEqual("San Mateo", map[5]);
                    Assert.AreEqual("CA", map[8]);
                    Assert.AreEqual("01-234", map[13]);
                }
            }
        }

        [Test]
        public void TestSelectMapWithLongKeys()
        {
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                // arrange
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var mapSfString = "OBJECT_CONSTRUCT('5','San Mateo', '8', 'CA', '13', '01-234')::MAP(INTEGER, VARCHAR)";
                    command.CommandText = $"SELECT {mapSfString}";

                    // act
                    var reader = (SnowflakeDbDataReader)command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var map = reader.GetMap<long, string>(0);
                    Assert.AreEqual(3, map.Count);
                    Assert.AreEqual("San Mateo", map[5L]);
                    Assert.AreEqual("CA", map[8L]);
                    Assert.AreEqual("01-234", map[13L]);
                }
            }
        }

        [Test]
        public void TestSelectMapOfObjects()
        {
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                // arrange
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var addressAsSFString = "OBJECT_CONSTRUCT('Warsaw', OBJECT_CONSTRUCT('prefix', '01', 'postfix', '234'), 'San Mateo', OBJECT_CONSTRUCT('prefix', '02', 'postfix', '567'))::MAP(VARCHAR, OBJECT(prefix VARCHAR, postfix VARCHAR))";
                    // var addressAsSFString = "{'city': 'San Mateo', 'state': 'CA'}::MAP(VARCHAR, VARCHAR)";
                    command.CommandText = $"SELECT {addressAsSFString}";

                    // act
                    var reader = (SnowflakeDbDataReader)command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var map = reader.GetMap<string, Zip>(0);
                    Assert.AreEqual(2, map.Count);
                    Assert.AreEqual(new Zip("01", "234"), map["Warsaw"]);
                    Assert.AreEqual(new Zip("02", "567"), map["San Mateo"]);
                }
            }
        }

        [Test]
        public void TestSelectMapOfArrays()
        {
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                // arrange
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var addressAsSFString = "OBJECT_CONSTRUCT('a', ARRAY_CONSTRUCT('b', 'c'))::MAP(VARCHAR, ARRAY(TEXT))";
                    // var addressAsSFString = "{'city': 'San Mateo', 'state': 'CA'}::MAP(VARCHAR, VARCHAR)";
                    command.CommandText = $"SELECT {addressAsSFString}";

                    // act
                    var reader = (SnowflakeDbDataReader)command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var map = reader.GetMap<string, string[]>(0);
                    Assert.AreEqual(1, map.Count);
                    CollectionAssert.AreEqual(new string[] {"a"}, map.Keys);
                    CollectionAssert.AreEqual(new string[] {"b", "c"}, map["a"]);
                }
            }
        }

        [Test]
        public void TestSelectMapOfLists()
        {
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                // arrange
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var addressAsSFString = "OBJECT_CONSTRUCT('a', ARRAY_CONSTRUCT('b', 'c'))::MAP(VARCHAR, ARRAY(TEXT))";
                    // var addressAsSFString = "{'city': 'San Mateo', 'state': 'CA'}::MAP(VARCHAR, VARCHAR)";
                    command.CommandText = $"SELECT {addressAsSFString}";

                    // act
                    var reader = (SnowflakeDbDataReader)command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var map = reader.GetMap<string, List<string>>(0);
                    Assert.AreEqual(1, map.Count);
                    CollectionAssert.AreEqual(new string[] {"a"}, map.Keys);
                    CollectionAssert.AreEqual(new string[] {"b", "c"}, map["a"]);
                }
            }
        }

        [Test]
        public void TestSelectAllUnstructuredTypesObject()
        {
            // arrange
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var allTypesObjectAsSFString = @"OBJECT_CONSTRUCT(
                        'StringValue', 'abc',
                        'CharValue', 'x',
                        'ByteValue', 15,
                        'SByteValue', -14,
                        'ShortValue', 1200,
                        'UShortValue', 65000,
                        'IntValue', 150150,
                        'UIntValue', 151151,
                        'LongValue', 9111222333444555666,
                        'ULongValue', 9111222333444555666,
                        'FloatValue', 1.23,
                        'DoubleValue', 1.23,
                        'DecimalValue', 1.23,
                        'BooleanValue', true,
                        'GuidValue', '57af59a1-f010-450a-8c37-8fdc78e6ee93',
                        'DateTimeValue', '2024-07-11 14:20:05'::TIMESTAMP_NTZ,
                        'DateTimeOffsetValue', '2024-07-11 14:20:05'::TIMESTAMP_LTZ,
                        'TimeSpanValue', '14:20:05'::TIME,
                        'BinaryValue', TO_BINARY('this is binary data', 'UTF-8')
                    )::OBJECT(
                        StringValue VARCHAR,
                        CharValue CHAR,
                        ByteValue SMALLINT,
                        SByteValue SMALLINT,
                        ShortValue SMALLINT,
                        UShortValue INTEGER,
                        IntValue INTEGER,
                        UIntValue INTEGER,
                        LongValue BIGINT,
                        ULongValue BIGINT,
                        FloatValue FLOAT,
                        DoubleValue DOUBLE,
                        DecimalValue REAL,
                        BooleanValue BOOLEAN,
                        GuidValue TEXT,
                        DateTimeValue TIMESTAMP_NTZ,
                        DateTimeOffsetValue TIMESTAMP_LTZ,
                        TimeSpanValue TIME,
                        BinaryValue BINARY
                    ), TO_BINARY('this is binary data', 'UTF-8')";
                    var bytesForBinary = Encoding.UTF8.GetBytes("this is binary data");
                    command.CommandText = $"SELECT {allTypesObjectAsSFString}";
                    var reader = (SnowflakeDbDataReader) command.ExecuteReader();
                    Assert.IsTrue(reader.Read());

                    // act
                    var allTypesObject = reader.GetObject<AllTypesClass>(0);

                    // assert
                    Assert.NotNull(allTypesObject);
                    Assert.AreEqual("abc", allTypesObject.StringValue);
                    Assert.AreEqual('x', allTypesObject.CharValue);
                    Assert.AreEqual(15, allTypesObject.ByteValue);
                    Assert.AreEqual(-14, allTypesObject.SByteValue);
                    Assert.AreEqual(1200, allTypesObject.ShortValue);
                    Assert.AreEqual(65000, allTypesObject.UShortValue);
                    Assert.AreEqual(150150, allTypesObject.IntValue);
                    Assert.AreEqual(151151, allTypesObject.UIntValue);
                    Assert.AreEqual(9111222333444555666, allTypesObject.LongValue);
                    Assert.AreEqual(9111222333444555666, allTypesObject.ULongValue); // there is a problem with 18111222333444555666 value
                    Assert.AreEqual(1.23f, allTypesObject.FloatValue);
                    Assert.AreEqual(1.23d, allTypesObject.DoubleValue);
                    Assert.AreEqual(1.23, allTypesObject.DecimalValue);
                    Assert.AreEqual(true, allTypesObject.BooleanValue);
                    Assert.AreEqual(Guid.Parse("57af59a1-f010-450a-8c37-8fdc78e6ee93"), allTypesObject.GuidValue);
                    Assert.AreEqual(DateTime.Parse("2024-07-11 14:20:05"), allTypesObject.DateTimeValue);
                    Assert.AreEqual(DateTimeOffset.Parse("2024-07-11 14:20:05 -07:00"), allTypesObject.DateTimeOffsetValue);
                    Assert.AreEqual(TimeSpan.Parse("14:20:05"), allTypesObject.TimeSpanValue);
                    CollectionAssert.AreEqual(bytesForBinary, allTypesObject.BinaryValue);
                }
            }
        }

        [Test]
        public void TestSelectArrayOfIntegers()
        {
            using (var connection =
                   new SnowflakeDbConnection(ConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var arrayOfIntegers = "ARRAY_CONSTRUCT(3, 5, 8)::ARRAY(INTEGER)";
                    command.CommandText = $"SELECT {arrayOfIntegers}";

                    // act
                    var reader = (SnowflakeDbDataReader) command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var array = reader.GetArray<int>(0);
                    Assert.AreEqual(3, array.Length);
                    CollectionAssert.AreEqual(new[] { 3, 5, 8 }, array);
                }
            }
        }

        [Test]
        public void TestSelectArrayOfLong()
        {
            using (var connection =
                   new SnowflakeDbConnection(ConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var arrayOfIntegers = "ARRAY_CONSTRUCT(3, 5, 8)::ARRAY(BIGINT)";
                    command.CommandText = $"SELECT {arrayOfIntegers}";

                    // act
                    var reader = (SnowflakeDbDataReader) command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var array = reader.GetArray<long>(0);
                    Assert.AreEqual(3, array.Length);
                    CollectionAssert.AreEqual(new[] { 3L, 5L, 8L }, array);
                }
            }
        }

        [Test]
        public void TestSelectArrayOfFloats()
        {
            using (var connection =
                   new SnowflakeDbConnection(ConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var arrayOfIntegers = "ARRAY_CONSTRUCT(3.1, 5.2, 8.11)::ARRAY(FLOAT)";
                    command.CommandText = $"SELECT {arrayOfIntegers}";

                    // act
                    var reader = (SnowflakeDbDataReader) command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var array = reader.GetArray<float>(0);
                    Assert.AreEqual(3, array.Length);
                    CollectionAssert.AreEqual(new[] { 3.1f, 5.2f, 8.11f }, array);
                }
            }
        }

        [Test]
        public void TestSelectArrayOfDoubles()
        {
            using (var connection =
                   new SnowflakeDbConnection(ConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var arrayOfIntegers = "ARRAY_CONSTRUCT(3.1, 5.2, 8.11)::ARRAY(DOUBLE)";
                    command.CommandText = $"SELECT {arrayOfIntegers}";

                    // act
                    var reader = (SnowflakeDbDataReader) command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    var array = reader.GetArray<double>(0);
                    Assert.AreEqual(3, array.Length);
                    CollectionAssert.AreEqual(new[] { 3.1d, 5.2d, 8.11d }, array);
                }
            }
        }

        [Test]
        [TestCaseSource(nameof(DateTimeConversionCases))]
        public void TestSelectDateTime(string dbValue, string dbType, DateTime expected, bool possibleForRawValue)
        {
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var rawValueString = $"'{dbValue}'::{dbType}";
                    var objectValueString = $"OBJECT_CONSTRUCT('Value', {rawValueString})::OBJECT(Value {dbType})";
                    command.CommandText = $"SELECT {rawValueString}, {objectValueString}";

                    // act
                    var reader = (SnowflakeDbDataReader) command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    if (possibleForRawValue)
                    {
                        var rawValue = reader.GetDateTime(0);
                        Assert.AreEqual(expected, rawValue);
                        Assert.AreEqual(expected.Kind, rawValue.Kind);
                    }
                    var wrappedValue = reader.GetObject<DateTimeWrapper>(1);
                    Assert.AreEqual(expected, wrappedValue.Value);
                    Assert.AreEqual(expected.Kind, wrappedValue.Value.Kind);
                }
            }
        }

        internal static IEnumerable<object[]> DateTimeConversionCases()
        {
            yield return new object[] { "2024-07-11 14:20:05", SFTimestampType.TIMESTAMP_NTZ.ToString(), DateTime.Parse("2024-07-11 14:20:05").ToUniversalTime(), true};
            yield return new object[] { "2024-07-11 14:20:05 +5:00", SFTimestampType.TIMESTAMP_TZ.ToString(), DateTime.Parse("2024-07-11 09:20:05").ToUniversalTime(), false};
            yield return new object[] {"2024-07-11 14:20:05 -7:00", SFTimestampType.TIMESTAMP_LTZ.ToString(), DateTime.Parse("2024-07-11 21:20:05").ToUniversalTime(), false};
        }

        [Test]
        [TestCaseSource(nameof(DateTimeOffsetConversionCases))]
        public void TestSelectDateTimeOffset(string dbValue, string dbType, DateTime? expectedRaw, DateTimeOffset expected)
        {
            using (var connection = new SnowflakeDbConnection(ConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    EnableStructuredTypes(connection);
                    var rawValueString = $"'{dbValue}'::{dbType}";
                    var objectValueString = $"OBJECT_CONSTRUCT('Value', {rawValueString})::OBJECT(Value {dbType})";
                    command.CommandText = $"SELECT {rawValueString}, {objectValueString}";

                    // act
                    var reader = (SnowflakeDbDataReader) command.ExecuteReader();

                    // assert
                    Assert.IsTrue(reader.Read());
                    if (expectedRaw != null)
                    {
                        var rawValue = reader.GetDateTime(0);
                        Assert.AreEqual(expectedRaw, rawValue);
                        Assert.AreEqual(expectedRaw?.Kind, rawValue.Kind);
                    }
                    var wrappedValue = reader.GetObject<DateTimeOffsetWrapper>(1);
                    Assert.AreEqual(expected, wrappedValue.Value);
                }
            }
        }

        internal static IEnumerable<object[]> DateTimeOffsetConversionCases()
        {
            yield return new object[] {"2024-07-11 14:20:05", SFTimestampType.TIMESTAMP_NTZ.ToString(), DateTime.Parse("2024-07-11 14:20:05").ToUniversalTime(), DateTimeOffset.Parse("2024-07-11 14:20:05Z")};
            yield return new object[] {"2024-07-11 14:20:05 +5:00", SFTimestampType.TIMESTAMP_TZ.ToString(), null, DateTimeOffset.Parse("2024-07-11 14:20:05 +5:00")};
            yield return new object[] {"2024-07-11 14:20:05 -7:00", SFTimestampType.TIMESTAMP_LTZ.ToString(), null, DateTimeOffset.Parse("2024-07-11 14:20:05 -7:00")};
        }

        private void EnableStructuredTypes(SnowflakeDbConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                // command.CommandText = "ALTER SESSION SET FEATURE_STRUCTURED_TYPES = enabled";
                // command.ExecuteNonQuery();
                // command.CommandText = "ALTER SESSION SET ENABLE_STRUCTURED_TYPES_IN_FDN_TABLES = true";
                // command.ExecuteNonQuery();
                command.CommandText = "ALTER SESSION SET DOTNET_QUERY_RESULT_FORMAT=JSON";
                command.ExecuteNonQuery();
            }
        }
    }
}
