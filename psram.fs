\ psram driver SPI

\ #include spi1.fs
\ #include gpio-simple.fs

PORTB 6 pin PSRAM_CS

: psram-write ( n buf addr -- errflg )
	0 PSRAM_CS set
	$02 spi!
	dup 16 rshift $FF and spi!
	dup  8 rshift $FF and spi!
	$FF and spi!
	swap 0 do dup i + c@ spi! loop
	drop
	1 PSRAM_CS set
	false
;

: psram-read ( n buf addr -- errflg )
	0 PSRAM_CS set
	$02 spi!
	dup 16 rshift $FF and spi!
	dup  8 rshift $FF and spi!
	$FF and spi!
	swap 0 do dup i + spi-read1 swap c! loop
	drop
	1 PSRAM_CS set
	false
;

10 buffer: psram_buf
: test-psram
	PSRAM_CS output
	1 PSRAM_CS set
	\ SPISettings(4000000, MSBFIRST, SPI_MODE0));
	spi-init

	0 PSRAM_CS set
	$9F spi!
	\ dummy address
	$00 spi!
	$00 spi!
	$00 spi!

	\ read ID
	spi-read1
	spi-read1
	spi-read1

	1 PSRAM_CS set

	." jedec id= " rot hex.2 swap hex.2 hex.2 cr

	s" 123456789" swap 0 psram-write drop
	9 psram_buf 0 psram-read drop
	psram_buf 9 type
;
