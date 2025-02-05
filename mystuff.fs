compiletoflash
#include words4.fs
#include 84mhz.fs
#include dictionary-tools.txt
#include utils.fs
#include fixpt-math-lib.fs
#include cycles.fs

: init
	84MHz
	cr ." system clock 84Mhz" cr
	init-cycles
;

compiletoram
