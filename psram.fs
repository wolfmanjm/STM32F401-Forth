\ psram driver SPI

\ #include spi1.fs
\ #include gpio-simple.fs

\ 1024 8 * 1024 * constant PSRAM_MEMSIZE \ 8MB ram size
1024 1024 * 2 * constant PSRAM_MEMSIZE \ 8MB ram size

PORTB 6 pin PSRAM_CS

: psram-cs ( flg -- )
	if 0 else 1 then
	PSRAM_CS set
;

: psram-cmd-addr ( addr cmd -- )
	spi-write1
	dup 16 rshift $FF and spi-write1
	dup  8 rshift $FF and spi-write1
	$FF and spi-write1
;

: psram-write ( n buf addr -- errflg )
	true psram-cs
	$02 psram-cmd-addr
	swap 0 do dup i + c@ spi-write1 loop
	drop
	spi-waitbusy
	false psram-cs
	false
;

: psram-read ( n buf addr -- errflg )
	true psram-cs
	$03 psram-cmd-addr
	swap 0 do dup i + spi-read1 swap c! loop
	drop
	spi-waitbusy
	false psram-cs
	false
;

: psram-dump ( n addr -- )
	cr
	true psram-cs
	$03 psram-cmd-addr
	1+ 1 do
		spi-read1 hex.2 space
		i 16 mod 0= if cr then
	loop
	false psram-cs
	cr
;

: REVERSE ( i*x i -- i*y ) 0 DO I ROLL LOOP ;
: apply-top ( i*x u.i xt -- j*x ) \ xt ( x -- k*x )
  swap dup 0= if 2drop exit then ( ... x xt u.i )
  rot >r 1- swap dup >r recurse ( R: x xt )
  2r> execute
;

10 buffer: psram_buf
: test-psram
	PSRAM_CS output
	false psram-cs
	\ SPISettings(4000000, MSBFIRST, SPI_MODE0));
	spi-init
	4000000 spi-baud

	true psram-cs
	0 $9F psram-cmd-addr
	\ read ID
	spi-read1
	spi-read1
	spi-read1
	spi-waitbusy
	false psram-cs

	." jedec id= " rot hex.2 swap hex.2 hex.2 cr

	." expect 123456789 using write and read buf" cr
	s" 123456789" swap 0 psram-write drop
	9 psram_buf 0 psram-read drop
	psram_buf 9 type cr

	." expect 1 2 3 4 5 6 using >spi and spi>" cr
	\ write data from the stack
	true psram-cs
	$10 $02 psram-cmd-addr 6 5 4 3 2 1 depth >spi
	spi-waitbusy
	false psram-cs

	\ read data onto the stack note will be in reverse order so addr 0 will be last on the stack
	true psram-cs
	$10 $03 psram-cmd-addr 6 spi>
	spi-waitbusy
	false psram-cs
	depth reverse depth 0 do . loop cr
	\ 6 ['] . apply-top cr
;

\ pseudo random
7 variable seed
: random    ( -- x )  \ return a 32-bit random number x
    seed @
    dup 13 lshift xor
    dup 17 rshift xor
    dup 5  lshift xor
    dup seed !
;

: setseed   ( x -- )  \ seed the RNG with x
    dup 0= or         \ map 0 to -1
    seed !
;

: psram-memtestwr ( seed -- )
	cr ." writing "
	setseed

	true psram-cs
	0 $02 psram-cmd-addr
	PSRAM_MEMSIZE 4 / 4 - 1 do
		random
		dup           $FF and spi-write1
		dup 8  rshift $FF and spi-write1
		dup 16 rshift $FF and spi-write1
			24 rshift $FF and spi-write1

		\ page boundary is 1024
		i 252 mod 0= if
			spi-waitbusy
			false psram-cs
			true psram-cs
			i 4 + 4 * $02 psram-cmd-addr
		then
		i 2048 mod 0= if ." ." then
	loop
	spi-waitbusy
	false psram-cs
;

0 variable memerr
: psram-memtestrd ( seed -- )
	cr
	." reading "
	setseed

	true psram-cs
	0 $03 psram-cmd-addr
	PSRAM_MEMSIZE 4 / 4 - 1 do
		i memerr !
		random
		spi-read1 over 	 		 $FF and <> if leave then
		spi-read1 over 8  rshift $FF and <> if leave then
		spi-read1 over 16 rshift $FF and <> if leave then
		spi-read1 swap 24 rshift $FF and <> if leave then

		\ page boundary is 1024
		i 252 mod 0= if
			spi-waitbusy
			false psram-cs
			true psram-cs
			i 4 + 4 * $03 psram-cmd-addr
		then
		i 2048 mod 0= if ." ." then
		0 memerr !
	loop
	memerr @ 0<> if ." memory error @ " memerr @ . then
	spi-waitbusy
	false psram-cs
;

: memtest
	." testing " PSRAM_MEMSIZE 1024 / . ." KBytes" cr
	PSRAM_CS output
	false psram-cs
	spi-init
	4000000 spi-baud
	$1357
	begin
		dup psram-memtestwr
		dup psram-memtestrd
		$1234 +
	key? until
;
