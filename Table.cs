using System;
using System.Collections.Generic;
using System.Linq;

namespace Emporium {
	
	public class Table {

		public string tableName { get; private set; }
		public List<Column> tableColumns {get; private set;}
		public List<Row> Rows { get; private set; }
		public bool hasPrimaryColumn { get; private set; }

		public Row LastRow {
			get {
				return Rows.isEmpty() ? null : Rows.Last ();
			}
		}

		public Table (BookShelf shelf, string name, Action completionHandler = null) {
			//Initialize properties
			tableName = name;
			tableColumns = new List<Column> ();
			Rows = new List<Row> ();
			//Fetch row info
			new Query (shelf, QueryType.DIRECT, tableName)
				.DirectCommand ("DESCRIBE "+tableName)
				.DirectExecute ((List<Dictionary<string, string>> dump) => {
					//Iterate through column data
					dump.ForEach(tableColumnInfo => {
						//Get column data
						bool primary = tableColumnInfo["Key"].Contains("PRI");
						Column column = new Column(tableColumnInfo["Field"], primary);
						//Set hasPrimaryColumn
						if (primary) hasPrimaryColumn = true;
						//Add column to tableColumns
						tableColumns.Add(column);
					});
			});
			//Populate row data
			new Query(shelf, QueryType.SELECT, tableName, false)
				.Execute((List<Dictionary<string, string>> tableDump) => {
					tableDump.Enumerate(row => {
						Rows.Add(new Row(this, row));
					}, completionHandler);
			});
		}

		public void Add (List<Criterion> row, int autoincrementID = 0) { //Add Row
			Row toAdd = new Row(this, row.GenerateDictionary());
			Rows.Add(toAdd);
			if (autoincrementID > 0 && hasPrimaryColumn) toAdd.PrimaryColumn.SetValue (autoincrementID.ToString());
		}

		public void Remove (Criterion criterion) { //Remove Row
			List<Row> toRemove = Rows.Where(tableRow => tableRow[criterion.key].value == criterion.value).ToList();
			toRemove.ForEach (item => Rows.Remove (item));
		}

		public void Remove (List<Criterion> criteria) { //Remove rows
			List<Row> toRemove = Rows.Where(tableRow => criteria.All(criterion => tableRow[criterion.key].value == criterion.value)).ToList();
			toRemove.ForEach (item => Rows.Remove (item));
		}

		public void Update (List<Criterion> whereDirectives, params Criterion[] updateDirectives) { //Update Row
			//Empty checking
			if (updateDirectives.Length == 0) return;
			//Select rows that need update
			List<Row> toUpdate = Rows.Where(row => whereDirectives.All(wdir => row[wdir.key].ToString() == wdir.value)).ToList();
			//Update rows
			toUpdate.ForEach (item => {
				updateDirectives.ToList().ForEach(upd => {
					item[upd.key].SetValue(upd.value); //Use a dud DataColumn
				});
			});
		}

		public Dictionary<string, string> Select (List<string> filters = null, params Criterion[] whereDirectives) { //Select row
			var ret = SelectMultiple (filters, whereDirectives);
			return ret.isEmpty() ? null : ret.First();
		}

		public List<Dictionary<string, string>> SelectMultiple (List<string> filters = null, params Criterion[] whereDirectives) { //Select rows
			//Select rows that need update
			List<Row> toSelect = new List<Row>();
			toSelect = whereDirectives.isEmpty() ? Rows : Rows.Where(row => whereDirectives.All(wdir => row[wdir.key].SafeToString() == wdir.value)).ToList();
			//Filter rows
			List<Dictionary<string, string>> ret = new List<Dictionary<string, string>>();
			//Empty checking
			if (toSelect.isEmpty()) return ret;
			//Filter rows
			ret = toSelect.Select(row => new Dictionary<string, string>(row.AsDictionary)).ToList(); //Dereference and clone
			//Curate filters then filter rows
			if (filters != null) {
				if (!filters.isEmpty ()) {
					tableColumns.Select(item => item.key).Where (key => !filters.Contains(key)).ToList ().ForEach (removeKey => {
						ret.ForEach(dict => dict.Remove (removeKey));
					});
				}
			}
			//Return collection;
			return ret;
		}

		public void Clear () {
			tableColumns.Clear ();
			Rows.Clear ();
			tableName = "";
			tableColumns = null;
			Rows = null;
		}
	}

	public class Column {
		public string key;
		public bool isPrimary;
		public Column (string Key, bool IsPrimary) {
			key = Key;
			isPrimary = IsPrimary;
		}
		public Column (Column origin) {
			key = origin.key;
			isPrimary = origin.isPrimary;
		}
	}

	public class DataColumn : Column {
		public string value;
		public DataColumn (string Key, bool IsPrimary, string Value) : base(Key, IsPrimary) {
			value = Value;
		}
		public DataColumn (Column column, string Value = "") : base (column) {
			value = Value;
		}
		public override string ToString () {
			return value;
		}
		public void SetValue (string Value) {
			value = Value;
		}
	}

	public class Row { //Contains data for each column
		public Table table {get; private set;}
		public Dictionary<string, DataColumn> Data {get; private set;}
		public DataColumn this [string index] {
			get {
				if (Data.ContainsKey (index))
					return Data [index];
				else
					return null;
			}
		}
		public DataColumn PrimaryColumn {
			get {
				return Data.Select (kvp => kvp.Value).First (col => col.isPrimary);
			}
		}
		public Dictionary<string, string> AsDictionary {
			get {
				return Data.Values.ToDictionary (val => val.key, val => val.value);
			}
		}
		public Row (Table Table, Dictionary<string, string> data) {
			//Initialize properties
			table = Table;
			Data = new Dictionary<string, DataColumn> ();
			//Populate data
			foreach (KeyValuePair<string, string> kvp in data) {
				//Get the column from string
				Column tableColumn = kvp.Key.asColumn (table);
				//Check to make sure it's correct
				if (tableColumn == null)
					continue;
				//Add it
				Data.Add (kvp.Key, new DataColumn (tableColumn, kvp.Value));
			}
			//Check empties for primary key
			Table.tableColumns.ForEach (col => {
				//Add any keys that weren't supplied
				if (!Data.ContainsKey(col.key)) {
					if (col.isPrimary) {
						//Get last row in table
						int lastInTable = Table.LastRow == null ? 0 : Table.LastRow[col.key].value.asInt();
						//Set new last row
						Data.Add(col.key, new DataColumn(col, (lastInTable+1).ToString()));
					}
					else {
						//Empty column
						Data.Add(col.key, new DataColumn(col));
					}
				}
			});
		}
	}
	public struct Criterion {
		public string key;
		public string value;
		public string hints;
		public Criterion (string Key, string Value) {
			key = Key;
			value = Value;
			hints = "";
		}
	}
}