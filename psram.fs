\ psram driver SPI

\ #include spi1.fs
\ #include gpio-simple.fs

#require spi1.fs
#require gpio-simple.fs

1024 8 * 1024 * constant PSRAM_MEMSIZE \ 8MB ram size

PORTB 6 pin PSRAM_CS

\ enable and disable cs
: psram-cs ( flg -- )
	if 0 else 1 then
	PSRAM_CS set
;

\ writes command and address
: psram-cmd-addr ( addr cmd -- )
	spi-write1
	dup 16 rshift $FF and spi-write1
	dup  8 rshift $FF and spi-write1
	$FF and spi-write1
;

\ buffer based data
: psram-write ( n buf addr -- )
	true psram-cs
	$02 psram-cmd-addr
	swap 0 do dup i + c@ spi-write1 loop
	drop
	spi-waitbusy
	false psram-cs
;

: psram-read ( n buf addr -- )
	true psram-cs
	$03 psram-cmd-addr
	swap 0 do dup i + spi-read1 swap c! loop
	drop
	spi-waitbusy
	false psram-cs
;

\ stack based data
: >psram ( d1..dn n addr -- )
	true psram-cs
	$02 psram-cmd-addr >spi
	spi-waitbusy
	false psram-cs
;

: psram> ( n addr -- d1..dn )
	true psram-cs
	$03 psram-cmd-addr
	spi>
	spi-waitbusy
	false psram-cs
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

\ test utils
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

	\ read id
	true psram-cs
	0 $9F psram-cmd-addr
	\ read MFD(1), KGD(1), EID(7)
	9 spi>
	spi-waitbusy
	false psram-cs
	9 REVERSE
	." MFD: " dup hex.2 space $0D <> if ." Bad MFD" then \ $0D
	." KGD: " dup hex.2 space $5D <> if ." Bad KGD" then \ $5D
	." Cap: " dup 5 rshift %111 and hex.2 space
	." EID: " %00011111 and hex.2
	hex.2 hex.2 hex.2 hex.2 hex.2 hex.2
	cr


	." expect 123456789 using write and read buf" cr
	s" 123456789" swap 0 psram-write
	9 psram_buf 0 psram-read
	psram_buf 9 type cr

	." expect 1 2 3 4 5 6 using >spi and spi>" cr
	\ write data from the stack to address 16
	6 5 4 3 2 1 depth $10 >psram

	\ read data onto the stack note will be in reverse order so addr 16 will be last on the stack
	6 $10 psram>
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

	PSRAM_MEMSIZE 4 / 4 - 1 do
		true psram-cs
		i 1- 4 * $02 psram-cmd-addr
		random
		dup           $FF and spi-write1
		dup 8  rshift $FF and spi-write1
		dup 16 rshift $FF and spi-write1
			24 rshift $FF and spi-write1
		spi-waitbusy
		false psram-cs
		i 262144 mod 0= if ." ." then
	loop
;

0 variable memerr
: psram-memtestrd ( seed -- )
	cr
	." reading "
	setseed

	PSRAM_MEMSIZE 4 / 4 - 1 do
		true psram-cs
		i 1- 4 * $03 psram-cmd-addr
		i memerr !
		random
		spi-read1 over 	 		 $FF and <> if leave then
		spi-read1 over 8  rshift $FF and <> if leave then
		spi-read1 over 16 rshift $FF and <> if leave then
		spi-read1 swap 24 rshift $FF and <> if leave then
		spi-waitbusy
		false psram-cs
		i 262144 mod 0= if ." ." then
		0 memerr !
	loop
	memerr @ 0<> if
		false psram-cs
		." memory error @ " memerr @ 4 * .
	then
;

: ps-ram-memtest
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
