using System;
using System.Collections.Generic;

namespace Emporium {
	
	public static class Debug {

		public static void PrimoInitialize (BookShelf shelf) {
			shelf.Log("Debug First Initialize called");
			//Call Emporium-independent tests here
		}

		public static void PostInitialize (BookShelf shelf) {
			shelf.Log("Debug Second Initialize called");
			//Call Emporium-dependent tests here
		}

		static void TestA (BookShelf shelf) {
			List<Dictionary<string, int>> dump = new List<Dictionary<string, int>> ();
			dump.Add (new Dictionary<string, int>{{"name", 22}, {"age", 13}});
			dump.Add (new Dictionary<string, int>{{"name", 35}, {"age", 17}});
			shelf.Log(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(dump));
		}

		static void TestB (BookShelf shelf) {
			new Query (shelf, QueryType.SELECT, "discretebooks", false)
				.Execute ((List<Dictionary<string, string>> dump) => {
					shelf.Log(dump.JSONEncode());
				});
		}

		static void TestC (BookShelf shelf) {
			//Create notification payload
			Dictionary<string, string> apsPayload = Extensions.GenerateResponseCollection("alert", "sound");
			//Populate notification payload
			apsPayload["alert"] = "a message";
			if (ConstantFactory.APNS_BADGE_NUMBER > 0) apsPayload.Add("badge", ConstantFactory.APNS_BADGE_NUMBER.ToString());
			apsPayload["sound"] = "default";
			//Generate supplementary payload
			Dictionary<string, string> supplementaryPayload = Extensions.GenerateResponseCollection("supplementary");
			//Populate supplementary payload
			supplementaryPayload["supplementary"] = "22";
			//Create push payload
			Dictionary<string, object> pushPayload = new Dictionary<string, object>();
			//Populate push payload
			pushPayload.Add("aps", apsPayload);
			pushPayload.Add("supplementary", "22");
			//Generate JSON string
			shelf.Log(pushPayload.JSONEncode().JSONFormat());
		}

		static void TestD (BookShelf shelf) {
			shelf.Log(string.Format(ConstantFactory.WishlistMessage, "Calculoso"));
		}

		static void TestF (BookShelf shelf) {
			BookKeeper.SendEmail (shelf, ConstantFactory.AdminContacts, ConstantFactory.EMAIL_OFFER_SUBJECT, Extensions.GenerateEmailBody("Lanre", "Calculus", "lanramazinglyawesome@gmail.com"));
			shelf.Log ("Sent out an Email");
		}

		static void TestG (BookShelf shelf) {
			string imgURL = "http://books.google.com/books/content?id=bJ5GBwAAQBAJ&printsec=frontcover&img=1&zoom=1&source=gbs_api";
			string isbn = "9780385382885";
			shelf.Log("Downloaded image from "+imgURL+" to "+imgURL.LoadWebpageImage (isbn.CoverPhotoDiskURI()));
		}

		static void TestH (BookShelf shelf) {
			new Query (shelf, QueryType.DIRECT, "discretebooks")
				.DirectCommand ("DESCRIBE discretebooks")
				.DirectExecute ((List<Dictionary<string, string>> dump) => {
					shelf.Log(dump.JSONEncode());
			});
		}

		static void TestI (BookShelf shelf) {
			new Query (shelf, QueryType.SELECT, "colleges")
				.SetFilterRow ("locationidentifier")
				.SetWhere ("collegecode", "5260")
				.Execute ((List<Dictionary<string, string>> dump) => {
					shelf.Log("<br");
					shelf.Log(dump.JSONEncode());
			});
		}

		static void TestJ (BookShelf shelf) {
			shelf.Log ("Test J");
			int newID = new Query (shelf, QueryType.INSERT, "users")
				.SetRow ("username", "Yswaggs", true)
				.Execute (true);
			shelf.Log(newID.ToString()+":"+EmporiumCore.Tables["users"].LastRow.AsDictionary.JSONEncode());
		}

		static void TestK (BookShelf shelf) {
			shelf.Log (ConstantFactory.AdminContacts [0]);
			shelf.Log (ConstantFactory.Ad ["Text"]);
		}
	}
}