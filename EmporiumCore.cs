/*
 *	Emporium
 *	Copyright (c) 2014 Yusuf Olokoba
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MySql.Data;
using MySql.Data.MySqlClient;

namespace Emporium {

	//TODO: Memory management, Try-catch dictionary fetches
	public static class EmporiumCore {
		//CREATE MORE ROBUST TABLE SYSTEM, USING DESCRIBE TABLE CALL
		public static MySqlConnection connection;
		public static bool optimized { get; private set; }
		public static bool initialized { get; private set; }

		private static readonly List<string> tableIDs = new List<string> {"colleges", "discretebooks", "marketplace", "messages", "offered", "participations", "remotenotifications", "users", "wishlist"};
		public static Dictionary<string, Table> Tables = new Dictionary<string, Table>();

		public static void Initialize (BookShelf shelf, bool withMemoryOptimization, Action WarmupCallback) {
			shelf.Log("Emporium: Trying to establish connection to MySQL server...");
			if (!initialized) {
				optimized = withMemoryOptimization;
				string connectionString;
				connectionString = string.Concat ("server=localhost;uid=", ConstantFactory.DB_User, ";pwd=", ConstantFactory.DB_Pass, ";database=", ConstantFactory.DB_Name, ";IgnorePrepare=false");

				try {
					connection = new MySqlConnection (connectionString);
					connection.Open ();
					shelf.Log("Emporium Initialized:  Sucessfully Connected");
					initialized = true;
					if (withMemoryOptimization) {
						PostInitialize (shelf, WarmupCallback);
					} else {
						WarmupCallback ();
					}
				} catch (MySqlException ex) {
					ex.ToString ();
				}
			} else {
				WarmupCallback ();
			}
		}

		static void PostInitialize (BookShelf shelf, Action WarmupCallback) {
			shelf.Log("Emporium Post Initialize Called");
			//Initialize Tables
			tableIDs.ForEach (tableID => {
				//Check if last ID
				bool isLast = tableID == tableIDs.Last();
				//Add new Table
				Tables.Add(tableID, new Table(shelf, tableID, isLast ? WarmupCallback : null));
			});
		}

		public static void StartUp (BookShelf shelf) {
			//This will start all Emporium Operations and Optimizations
			if (!optimized) {
				//Initialize and populate tables, then switch Emporium on
				PostInitialize (shelf, () => optimized = true);
			} else {
				if (shelf != null) shelf.Log ("Emporium is already running");
			}
		}

		public static void Shutdown (BookShelf shelf = null) {
			//This will switch off all Emporium optimizations and route all request directly to MySQL
			if (optimized) {
				if (shelf != null) shelf.Log("Shutting down Emporium");
				//Clear tables deeply
				foreach (KeyValuePair<string, Table> table in Tables) {
					table.Value.Clear ();
				}
				Tables.Clear ();
				//Switch Emporium off
				optimized = false;
			}
			else {
				if (shelf != null) shelf.Log("Emporium cannot be shutdown because it is not running");
			}
		}

		public static void Restart (BookShelf shelf) {
			//This can be used for clearing Emporium's cache in case of an error; it might be a slow process
			Shutdown (shelf);
			StartUp (shelf);
		}
	}

	public enum QueryType {
		INSERT = 0,
		SELECT = 1,
		UPDATE = 2,
		DELETE = 3,
		DIRECT = 4
	}
}
