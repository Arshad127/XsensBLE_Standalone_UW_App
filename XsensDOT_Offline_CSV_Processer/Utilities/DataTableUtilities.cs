using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XsensDOT_StandardLibrary;

namespace XsensDOT_Offline_CSV_Processer.Utilities
{
    public static class DataTableUtilities
    {
        /// <summary>
        /// Create the tables skeleton for storing the data from the csv files
        /// </summary>
        internal static DataTable SetUpInputDataTable(string xSensDotIdentifier, DataSet dataSet)
        {
            string tableName = "XSENS_MEASUREMENT_DATA_CSV_DOT_" + xSensDotIdentifier;

            // need to check for duplicate tables in the dataset.
            // be weary of the race conditions given there is only one dataset being access by two tables
            // ****CHECK RACE CONDITIONS*****
            if (dataSet == null)
            {
                dataSet = new DataSet();
            }
            else if (dataSet.Tables.Contains(tableName))
            {
                // remove duplicate tables else problemo
                dataSet.Tables.Remove(tableName);
            }

            DataTable csvDataTable = new DataTable(tableName);

            // Column
            csvDataTable.Columns.Add(Header.PacketCount + xSensDotIdentifier, typeof(int));

            // Unique Column & primary key column
            var dtColumn = new DataColumn();
            dtColumn.DataType = typeof(long);
            dtColumn.ColumnName = Header.SampleTimeFine.ToString(); // NO IDENTIFIER
            dtColumn.AutoIncrement = false;
            dtColumn.ReadOnly = true;
            dtColumn.Unique = true;
            csvDataTable.Columns.Add(dtColumn);

            // Workaround to create the unique primary key for this table
            csvDataTable.PrimaryKey = new DataColumn[] { csvDataTable.Columns[Header.SampleTimeFine.ToString()] }; // NO IDENTIFIER

            csvDataTable.Columns.Add(Header.Quat_W + xSensDotIdentifier, typeof(double));
            csvDataTable.Columns.Add(Header.Quat_X + xSensDotIdentifier, typeof(double));
            csvDataTable.Columns.Add(Header.Quat_Y + xSensDotIdentifier, typeof(double));
            csvDataTable.Columns.Add(Header.Quat_Z + xSensDotIdentifier, typeof(double));
            //csvDataTable.Columns.Add(Header.FreeAcc_X + xSensDotIdentifier, typeof(double));
            //csvDataTable.Columns.Add(Header.FreeAcc_Y + xSensDotIdentifier, typeof(double));
            //csvDataTable.Columns.Add(Header.FreeAcc_Z + xSensDotIdentifier, typeof(double));
            //csvDataTable.Columns.Add(Header.Status + xSensDotIdentifier, typeof(int));


            // add the table to the dataset
            dataSet.Tables.Add(csvDataTable);

            return csvDataTable;
        }
    }
}
