# DotChess

Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  

Licensed under the MIT License. https://opensource.org/licenses/MIT

Version 1.0.4

DotChess is an open source chess rules engine written in C# .NET. (.Net Standard 2.0)
It is meant to be simple and readable source code. It values readability over speed. (~1.5M moves per second)

Sources are hosted on GitHub at https://github.com/MenaceSan/DotChess
The secondary project documentation pages are at https://www.menasoft.com/dotchess or https://www.menasoft.com/blog/?page_id=542

This engine could be ported to C++ (relatively easily) for speed improvement. 
It has no single GUI. 
The consumer is meant to provide their own UI. The GUI might be some standalone app or might be a web site.

# Features:

- Enforce all standard chess rules on all moves. (Castle, EnPassant, Check, Promotion, Stalemate)
- Get a list of all valid moves from a board state for any / all pieces.
- Reads/Writes PGN files.
- Opening Move Db with > 50K master games.
- Read/Write FEN state
- Score future moves for best recommendation.
- Unit Tests for coverage of all basic features. 
- Unit Test tool for regeneration of the opening moves db.
- Multi threaded optimization for speeding up complex tests. Uses Parallel .NET name space. ~1.5M moves per second on my i7 CPU.

# Notes:

Rebuilding the OpeningDb.bin file can be done via the unit tests. Uncomment the [UnitTest] for BuildOpeningDbFile() and run it.

# Todo:
- UCI support for engine plugin to GUI apps like Arena.
- Better scoring optimization. Trimming futile score paths might be a huge CPU savings. Current scoring a rather brute force. So the results are good but expensive.
e.g. There are > 5M brute force tests to be performed for looking ahead 4 moves on the opening board. Many of those test paths could be obviously discarded, resulting in far fewer tests.
 

# Similar Projects:

https://stockfishchess.org/

https://github.com/official-stockfish/Stockfish

https://github.com/SavchukSergey/ChessRun

# Other Resources:
https://fontawesome.com/icons/chess-king?style=solid

http://www.playwitharena.de/
 

