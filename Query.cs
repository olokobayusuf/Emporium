/*
 *	Emporium
 *	Copyright (c) 2014 Yusuf Olokoba
 */

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using MySql.Data;
using MySql.Data.MySqlClient;

namespace Emporium {

	public class Query {

		public QueryType type { get; private set;}
		public string tableName { get; private set;}
		public bool usesOptimization { get; private set; }
		public string queryString;
		public string queryToken {
			get {
				string ret = "";
				switch (type) {
				case QueryType.INSERT:
					ret = string.Concat ("INSERT INTO ", tableName, " * VALUES #");
					break;
				case QueryType.SELECT:
					ret = string.Concat ("SELECT * FROM ", tableName); //DONT GRAB MORE ROWS THAN NECESSARY
					break;
				case QueryType.UPDATE:
					ret = string.Concat ("UPDATE ", tableName, " SET #");
					break;
				case QueryType.DELETE:
					ret = string.Concat ("DELETE FROM ", tableName);
					break;
				}
				return ret;
			}
		}

		//Directives
		private List<string> filterDirectives = new List<string>();
		private List<Criterion> whereDirectives = new List<Criterion>();
		private List<Criterion> rowDirectives = new List<Criterion>();

		private BookShelf shelf;

		public Query (BookShelf Shelf, QueryType queryType, string table, bool optimization = true) {
			shelf = Shelf;
			type = queryType;
			tableName = table;
			usesOptimization = optimization;
		}

		/// <summary>
		/// Use this to specify a criterion or criteria when selecting a specific row or rows in a table. You usually want to call this first before any SetRows or SetFilterRows. Cannot be used for INSERT.
		/// </summary>
		/// <returns>The where.</returns>
		/// <param name="db_key">Db key.</param>
		/// <param name="value">Value.</param>
		public Query SetWhere (string db_key, object value) {
			if (type == QueryType.INSERT) {
				shelf.Log("Cannot set Where directive for this QueryType");
				return this;
			}
			whereDirectives.Add(new Criterion(db_key, Convert.ToString(value)));
			return this;
		}



		/// <summary>
		/// Use this to set rows for INSERT or UPDATE operations. This must be called in correct table-structure sequence when calling insert.
		/// </summary>
		/// <param name="db_key">The row in the database table</param>
		/// <param name="value">The value to be inserted in the specified row</param>
		/// <param name="setFilter">If set to <c>true</c> also set as filter row.</param>
		public Query SetRow (string db_key, string value, bool setFilter = false) {
			if (type != QueryType.INSERT && type != QueryType.UPDATE) {
				shelf.Log("Cannot add rows, or filter rows, for this QueryType");
				return this;
			}
			if (setFilter) filterDirectives.Add (db_key);
			rowDirectives.Add (new Criterion (db_key, value));
			return this;
		}

		/// <summary>
		/// Use this to filter return data if using SELECT; or to filter what data gets put in the DB Table when using INSERT. If using INSERT, you could call SetRow and specify 'setFilter' as true.
		/// </summary>
		/// <returns>The filter row.</returns>
		/// <param name="db_key">Db key.</param>
		public Query SetFilterRow (string db_key) { //When using insert, call this then setRow
			if (type != QueryType.INSERT && type != QueryType.SELECT) {
				shelf.Log("Cannot add filter rows for this QueryType");
				return this;
			}
			filterDirectives.Add (db_key);
			return this;
		}

		public Query DirectCommand (string query) {
			if (type != QueryType.DIRECT) {
				if (shelf != null)
					shelf.Log ("Cannot set direct command query on non-direct command type");
			}
			queryString = query;
			return this;
		}

		public void DirectExecute (Action<List<Dictionary<string, string>>> callback) {
			if (type != QueryType.DIRECT) {
				if (shelf != null) shelf.Log ("Cannot perform direct execution on non-direct command type");
			}
			//Create execution command
			MySqlCommand cmd = new MySqlCommand();
			//Set command properties
			cmd.Connection = EmporiumCore.connection;
			cmd.CommandText = queryString;
			//Generate return collection 
			List<Dictionary<string, string>> ret = new List<Dictionary<string, string>> ();
			MySqlDataReader reader = cmd.ExecuteReader();
			while (reader.Read ()) {
				Dictionary<string, string> unit = new Dictionary<string, string>();
				for (int i = 0; i < reader.FieldCount; i++) {
					unit.Add (reader.GetName (i), reader.GetValue(i).ToString());
				}
				ret.Add (unit);
			}
			reader.Close ();
			callback (ret);
		}

		public void Execute () {
			if (type == QueryType.SELECT) {
				shelf.Log("Cannot return data with this QueryType and Execution Method");
				return;
			}
			//Update Emporium
			if (EmporiumCore.optimized && usesOptimization) {
				if (type == QueryType.INSERT) {
					int useless = Execute (true); //Already implemented elsewhere
					return; //So we don't duplicate inserts
				}
				if (type == QueryType.DELETE) { //Prone to Error because of unsafe select
					EmporiumCore.Tables[tableName].Remove(whereDirectives);
				}
				if (type == QueryType.UPDATE) { //ERROR //Android registerToken //Find and fix
					EmporiumCore.Tables [tableName].Update (whereDirectives, rowDirectives.ToArray());
				}
			}
			//Update MySQL
			if (ConstantFactory.PreferAsyncMySQLCalls) GenerateQueryCommand ().ExecuteNonQueryAsync ();
			else GenerateQueryCommand ().ExecuteNonQuery ();
		}

		public int Execute (bool autoIncrement) { //Returns last insertID
			//Type check
			if (type != QueryType.INSERT) {
				shelf.Log("Cannot fetch new insert ID for this QueryType");
				return 0;
			}
			//Perform insert to MySQL
			int newID = 0;
			MySqlCommand comm = GenerateQueryCommand();
			if (autoIncrement) {
				comm.CommandText += "; select last_insert_id();";
				newID = Convert.ToInt32 (comm.ExecuteScalar ());
			} else {
				if (ConstantFactory.PreferAsyncMySQLCalls) comm.ExecuteNonQueryAsync ();
				else comm.ExecuteNonQuery ();
			}
			//Update Emporium
			if (EmporiumCore.optimized && usesOptimization) {
				EmporiumCore.Tables [tableName].Add (rowDirectives, newID);
			}
			//Return
			return newID;
		}

		public void Execute (Action<Dictionary<string, string>> callback) { //Used for selecting a row
			if (type != QueryType.SELECT) {
				shelf.Log("Cannot execute with row callback on this QueryType");
				return;
			}

			if (usesOptimization && EmporiumCore.optimized) {
				callback (EmporiumCore.Tables [tableName].Select (filterDirectives, whereDirectives.ToArray ()));
			}
			else {
				Dictionary<string, string> ret = null;
				MySqlDataReader reader = GenerateQueryCommand ().ExecuteReader ();
				while (reader.Read ()) {
					if (ret == null) {
						ret = new Dictionary<string, string>();
					}
					for (int i = 0; i < reader.FieldCount; i++) {
						ret.Add (reader.GetName (i), reader.GetValue (i).ToString ());
					}
				}
				reader.Close ();
				callback (ret);
			}
		}

		public void Execute (Action<List<Dictionary<string, string>>> callback) { //Used for dumping tables
			if (type != QueryType.SELECT) {
				shelf.Log("Cannot execute with row-dump callback on this QueryType");
				return;
			}

			if (usesOptimization && EmporiumCore.optimized) {
				callback (EmporiumCore.Tables [tableName].SelectMultiple (filterDirectives, whereDirectives.ToArray ()));
			}
			else {
				List<Dictionary<string, string>> ret = new List<Dictionary<string, string>> ();
				MySqlDataReader reader = GenerateQueryCommand().ExecuteReader();
				while (reader.Read ()) {
					Dictionary<string, string> unit = new Dictionary<string, string>();
					for (int i = 0; i < reader.FieldCount; i++) {
						unit.Add (reader.GetName (i), reader.GetValue(i).ToString());
					}
					ret.Add (unit);
				}
				reader.Close ();
				callback (ret);
			}
		}

		private MySqlCommand GenerateQueryCommand () {
			//Create execution command
			MySqlCommand cmd = new MySqlCommand();
			cmd.Connection = EmporiumCore.connection;
			//Cheap way of monitoring preparations
			bool prepareWheres = false;
			bool prepareRows = false;
			//Build query
			string prepareQ = queryToken;
			//Boil insertRow
			if (type == QueryType.INSERT || type == QueryType.SELECT) {
				string irowstr = type == QueryType.SELECT ? "*" : "";
				if (filterDirectives.Count > 0) {
					//"Adding insert rows".Log ();
					irowstr = (type == QueryType.INSERT ? "(" : "") + string.Join (", ", filterDirectives.ToArray ()) + (type == QueryType.INSERT ? ")" : "");
				}
				prepareQ = prepareQ.Replace ("*", irowstr);
			}
			//Boil Where directives
			if (type != QueryType.INSERT) {
				string wherePreps = "";
				if (whereDirectives.Count > 0) {
					prepareQ += " WHERE ^";
					//"Adding where directives".Log ();
					//string hints: OR. Default AND.
					wherePreps = string.Join(" AND ", whereDirectives.Select (item => item.key+"=@" + item.key).ToArray());
					prepareWheres = true;
				}
				prepareQ = prepareQ.Replace ("^", wherePreps);
			}
			//Boil row directives
			if (type == QueryType.INSERT || type == QueryType.UPDATE) {
				string rowPreps = "";
				if (rowDirectives.Count > 0) {
					rowPreps = type == QueryType.INSERT ? ("(" + string.Join(", ", rowDirectives.Select (item => "@" + item.key).ToArray()) + ")") : (string.Join(", ", rowDirectives.Select (item => item.key+"=@" + item.key).ToArray()));
					prepareRows = true;
				}
				prepareQ = prepareQ.Replace ("#", rowPreps);
			}
			//Assign command
			cmd.CommandText = prepareQ;
			//Prepare
			if (prepareWheres || prepareRows) {
				if (prepareWheres) {
					whereDirectives.ForEach (item => cmd.Parameters.AddWithValue (("@" + item.key), item.value));
				}
				if (prepareRows) {
					rowDirectives.ForEach(item => cmd.Parameters.AddWithValue(("@"+item.key), item.value));
				}
				cmd.Prepare ();
			}
			//Return command
			return cmd;
		}
	}
}
