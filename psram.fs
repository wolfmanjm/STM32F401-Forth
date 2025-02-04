\ psram driver SPI

\ #include spi1.fs
\ #include gpio-simple.fs

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

10 buffer: psram_buf
: test-psram
	PSRAM_CS output
	false psram-cs
	\ SPISettings(4000000, MSBFIRST, SPI_MODE0));
	spi-init

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
	$10 $02 psram-cmd-addr 6 5 4 3 2 1 6 >spi
	spi-waitbusy
	false psram-cs

	\ read data onto the stack note will be in reverse order so addr 0 will be last on the stack
	true psram-cs
	$10 $03 psram-cmd-addr 6 spi>
	spi-waitbusy
	false psram-cs
	6 0 do . loop cr
;
