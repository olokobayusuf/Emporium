# Emporium
Emporium is a lightweight, highly-optimized abstraction of a MySQL data server built in C#. Built for the BookSwapp app.

# The Emporium Advantage
Emporium utilizes RAM caching to create a persistent clone of high-access-volume data in a MySQL database. Data is stored in hashtables (System.Collections.Generic.Dictionary) which have constant time retrieval. When changes are made to tables (INSERT INTO and DELETE FROM), Emporium stores the change in memory, returns to the client (to keep server requests as fast as possible), then updates MySQL with the change.

# Room for Improvement
Emporium must be optimized to scale beyond databases with tens of thousands of records. In its current implementation, it is extremely heavy on memory consumption. This can be fixed by implementing a smart caching solution:

# Smart Caching
Ideally, Emporium should only cache parts of the database with frequent accesses. This could be easily implemented by using a request monitor to give preference to tables (or regions thereof) that can be cached and those that should be dropped from the cache. The preference system can then provide its rankings to a CacheProvider which runs at several-minute intervals to clear the store and update the cache. The CacheProvider will run on a separate thread and will forward queries directly to MySQL when it is rebuilding the cache.

# Benchmarks
Dumping a table of colleges in the US, with over 3,300 entries, took 12ms on the standard MySQL implementation. Using Emporium, the dump took anywhere between 0-2ms.

# Debug
See the Debug.cs class for how Emporium would typically be used by an ASP.NET database server.
