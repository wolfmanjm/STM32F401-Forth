\ 24lc256 i2c eeprom driver

#require i2c.fs

$50 constant EEPROM_ADDRESS

: eeprom-ready? ( -- flg )
    i2c-start if false exit then
	$00 EEPROM_ADDRESS i2c-send1 if false exit then
    i2c-stop
    true
;

: eeprom-init ( -- errflg )
	EEPROM_ADDRESS i2c-deviceready? not if ." EEPROM not found at address: " EEPROM_ADDRESS hex. true exit then
	eeprom-ready? not
;

: eeprom-setaddress ( addr -- errflg )
    dup 8 rshift EEPROM_ADDRESS i2c-send1 if drop true exit then
    $FF and i2c!
;

: eeprom-read1 ( addr -- data )
    i2c-start if drop $8000 exit then
    eeprom-setaddress if $8000 exit then
    i2c-restart if $8000 exit then
    EEPROM_ADDRESS i2c-read1
;

: eeprom-write1 ( data addr -- errflg )
    i2c-start if 2drop true exit then
    eeprom-setaddress if drop true exit then
    i2c! if true exit then
    i2c-stop
	begin eeprom-ready?	until
    false
;

\ bulk read
: eeprom-read ( n buf maddr -- errflg )
 	EEPROM_ADDRESS i2c-memory-read
;

\ can only write 64 bytes at a time, and must not cross 64 byte page boundary in one write
: eeprom-write ( n buf addr -- errflg )
	\ check for page boundary and break up writes accordingly
	dup 64 mod 64 swap - >r
	2 pick r@ > if
		r@ 2 pick 2 pick recurse if rdrop 2drop drop true exit then
		rot r@ - rot r@ + rot r> + recurse if 2drop drop true exit then
		false exit
	then
	rdrop

	\ do actual write
    i2c-start if 2drop drop true exit then
    eeprom-setaddress if 2drop true exit then
 	swap 0 do
 		dup i + c@ i2c! if unloop drop exit then
 	loop
 	drop
    i2c-stop
	begin eeprom-ready?	until
    false
;

: test-fill
	cr
	i2c-init
	eeprom-init if ." eeprom failed to init" exit then
	32 0 do
		i 1+ i eeprom-write1 if ." eeprom failed to write at addr: " i . unloop exit then
		i .
	loop
;

: test-read1
	cr
	i2c-init
	eeprom-init if ." eeprom failed to init" exit then
	32 0 do
		i eeprom-read1
		i . ." : " . cr
		eeprom-ready? not if ." not ready " then
	loop
;

: test-readaddr ( addr -- )
	cr
	i2c-init
	eeprom-init if drop ." eeprom failed to init" exit then
	eeprom-read1 . cr
;

200 buffer: tmp
: test-readbulk ( n -- )
	cr
	i2c-init
	eeprom-init if drop ." eeprom failed to init" exit then
	dup
	tmp 0 eeprom-read if drop ." eeprom failed to read" exit then
	0 do
		i tmp + c@
		i . ." : " . cr
	loop
;

: test-read32
	cr
	i2c-init
	eeprom-init if ." eeprom failed to init" exit then
	32 tmp 0 eeprom-read if ." eeprom failed to read" exit then
	32 0 do
		i tmp + c@
		i . ." : " . cr
	loop
;

: test-writebulk32 ( -- )
	i2c-init
	eeprom-init if ." eeprom failed to init" exit then
	32 0 do
		32 i - tmp i + c!
	loop

	32 tmp 0 eeprom-write if ." eeprom failed to write" exit then
;

: test-writebulk200 ( -- )
	i2c-init
	eeprom-init if ." eeprom failed to init" exit then
	200 0 do
		i 1+ tmp i + c!
	loop

	200 tmp 0 eeprom-write if ." eeprom failed to write" exit then

	200 0 do
		i eeprom-read1 i 1+ <> if ." error " then
	loop
;

