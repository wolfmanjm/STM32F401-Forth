84000000 constant F_CPU  \ 84MHz if set
100000 constant I2C_RATE

\ I2C1 is connected to pins PB8 SCL and PB9 SDA
: i2c-init
	1 I2C1 _iCR1 bic!                \ disable I2C
	1 21 lshift RCC _rAPB1ENR bis! 	 \ enable I2C CLOCK
	1 1 lshift RCC _rAHB1ENR bis!	 \ enable GPIOB CLOCK
	MODE_Alternate 8 PORTB set-moder \ Alternate function for PB8 and PB9
	MODE_Alternate 9 PORTB set-moder
	1 8 lshift 1 9 lshift or PORTB _pOTYPER bis! \ output open drain
	SPEED_VERYHIGH 8 PORTB set-opspeed \ High Speed for PIN PB8 and PB9
	SPEED_VERYHIGH 9 PORTB set-opspeed
	1 16 lshift 1 18 lshift or PORTB _pPUPDR bis! \ Pull up for PIN PB8 and PB9
	4 8 PORTB set-alternate                       \ set AF4 for PB8 and PB9
	4 9 PORTB set-alternate

	1 15 lshift I2C1 _iCR1 bis! \ reset I2C
	1 15 lshift I2C1 _iCR1 bic! \ set normal operation
	%111111 I2C1 _iCR2 bic!  \ clear first
	hclk 2000000 / I2C1 _iCR2 bis! \ set pclk1 frequency in MHz in i2c reg
	%111111111111 I2C1 _iCCR bic!  \ clear first
	$0096 I2C1 _iCCR bis! \ set CCR from datasheet for 100KHz with 42Mhz periph clk
	43 I2C1 _iTRISE !     \ set trise = (1000ns / Tpclk1(ns)) +1
	1 I2C1 _iCR1 bis!     \ enable I2C
;

0 variable i2c-timeout
0 variable i2c-tmo_tick
0 variable i2c-tmo_flg

: i2c-settmo ( tmo -- )
	i2c-timeout !
	false i2c-tmo_flg !
	tick# @ i2c-tmo_tick !
;

: i2c-checktmo ( -- flg )
	tick# @ i2c-tmo_tick @ - i2c-timeout @ > dup i2c-tmo_flg !
;

: i2c-tmo? ( -- flg )
	i2c-tmo_flg @
;

: i2c-disablepos
	%100000000000 I2C1 _iCR1 bic!
;

: i2c-enablepos
	%100000000000 I2C1 _iCR1 bis!
;

: i2c-disableack
	%10000000000 I2C1 _iCR1 bic!
;

: i2c-enableack
	%10000000000 I2C1 _iCR1 bis!
;

: i2c-busy? ( -- flg )
	%10 I2C1 _iSR2 bit@
;

: i2c-waitonbusy ( -- flg )
	25 i2c-settmo
	begin i2c-busy? not i2c-checktmo or until
	i2c-tmo?
;

: i2c-restart ( -- errflg )
	$100 I2C1 _iCR1 bis!   		\ Generate START
	1000 i2c-settmo
	begin %1 I2C1 _iSR1 bit@ i2c-checktmo or until 	\ wait for SB bit to set or timeout
	i2c-tmo? if
		$100 I2C1 _iCR1 bit@ if ." wrong start" else ." timed out" then
		true
	else
		false
	then
;

: i2c-start ( -- errflg )
	i2c-waitonbusy if true exit then
	i2c-disablepos

	i2c-enableack \ Enable the ACK
	i2c-restart
;


: i2c-stop ( -- )
	%1000000000 I2C1 _iCR1 bis! \ Stop I2C
;

: i2c-afset? ( -- flg )
	$400 I2C1 _iSR1 bit@
;

: i2c-sendaddr ( slaveaddr -- errflg )
	I2C1 _iDR !  \  send the address

	1000 i2c-settmo
	begin
		%10 I2C1 _iSR1 bit@     \ wait for ADDR bit to set
		i2c-afset? or 			\ or AF set
		i2c-checktmo or         \ or timeout
    until

    i2c-afset? if
    	$400 I2C1 _iSR1 bic! 	\ clear AF bit
    	i2c-stop
    	true
    else
		i2c-tmo?
    then
;

: i2c-clearaddr ( -- )
	I2C1 _iSR1 @ drop
	I2C1 _iSR2 @ drop        \ read SR1 and SR2 to clear the ADDR bit
;

: i2c-7bitaddwrite ( addr -- addr )
	1 lshift
;

: i2c-7bitaddread ( addr -- addr )
	1 lshift 1 or
;

: i2c-deviceready? ( addr -- flg )
	i2c-start if exit then

	i2c-7bitaddwrite I2C1 _iDR !  \  send the address
	begin
		%10 I2C1 _iSR1 bit@  \ test ADDR bit
		i2c-afset?           \ test AF bit
		or				     \ wait for either
    until

	%10 I2C1 _iSR1 bit@ if \ it was addr
		i2c-stop
		i2c-clearaddr
		i2c-waitonbusy drop
		true
	else					\ it was AF
		i2c-stop
		$400 I2C1 _iSR1 bic! \ clear AF bit
		i2c-waitonbusy drop
		false
	then
;

: i2c-waitfortxbtf ( -- flg )
	begin
		%100 I2C1 _iSR1 bit@ 			\ wait for BTF bit set
		i2c-afset? or					\ or NACK
		i2c-checktmo or 				\ or timeout
	until

	i2c-afset? if
		$400 I2C1 _iSR1 bic! 			\ clear AF bit
		i2c-stop
		true
		exit
    else
		i2c-tmo?
    then
;

: i2c-waitforrxbtf ( -- flg )
	begin
		%100 I2C1 _iSR1 bit@ 			\ wait for BTF bit set
		i2c-checktmo or 				\ or timeout
	until
	i2c-tmo?
;

: i2c-waitfortxe ( -- flg )
	begin
		%10000000 I2C1 _iSR1 bit@ 		\ wait for TXE bit set
		i2c-afset? or					\ or NACK
		i2c-checktmo or 				\ or timeout
	until
	\ if it was NACK
	i2c-afset? if
		$400 I2C1 _iSR1 bic! 			\ clear AF bit
		i2c-stop
		true
		exit
	then
	i2c-tmo?
;

\ write single byte
: i2c! ( data -- errflg )
	1000 i2c-settmo
	i2c-waitfortxe if drop true exit then
	I2C1 _iDR !                         \ write data
	i2c-waitfortxbtf
;

\ send single byte to specified address, sets up for further writes using i2c!
: i2c-send1 ( c slaveaddr -- errflg )
	i2c-7bitaddwrite i2c-sendaddr if drop true exit then
	i2c-clearaddr
	i2c!
;

\ optimized send 2 bytes to specified address and stop
: i2c-send2stop ( n2 n1 slaveaddr -- errflg )
    i2c-start if 2drop drop true exit then
    i2c-7bitaddwrite i2c-sendaddr if 2drop true exit then
	i2c-clearaddr
	1000 i2c-settmo
	i2c-waitfortxe if 2drop true exit then		\ if error
	I2C1 _iDR !                         		\ write first byte
	%100 I2C1 _iSR1 bit@ if						\ if BTF set send second byte
		I2C1 _iDR ! 							\ write second byte
		i2c-waitfortxbtf if true exit then
	else
		i2c-waitfortxbtf if drop true exit then
		i2c-waitfortxe if drop true exit then
		I2C1 _iDR !                     		\ write second byte
		i2c-waitfortxbtf if true exit then
	then

	i2c-stop
	false
;

: i2c-sendbufstop ( n c-bufaddr slaveaddr -- errflg )
	2 pick 2 < if ." ERROR Must have more than 1 in buffer" 2drop drop true exit then
    i2c-start if 2drop drop true exit then
	i2c-7bitaddwrite i2c-sendaddr if 2drop true exit then
	i2c-clearaddr
	over						    \ -- n bufaddr n
	0 do 					        \ for 0 to n-1, -- n bufaddr
		dup i + c@
		i2c! if unloop 2drop true exit then     \ write data or exit on error
	loop  \ -- n bufaddr
	i2c-stop
	2drop
	false
;

\ returns true if error occured
: i2c-waitforrxne ( -- errflg )
	begin
		$40 I2C1 _iSR1 bit@ 			\ wait for RXNE to set
		$10 I2C1 _iSR1 bit@	or			\ Check if a STOPF is detected
		i2c-checktmo or 				\ or timeout
	until

	$10 I2C1 _iSR1 bit@	if				\ if a STOPF was detected
		$10 I2C1 _iSR1 bic!  			\ clear STOPF
		true exit
	then
	i2c-tmo? if
		true exit
	then
	false
;

\ read one byte from specified address and stop, return $8000 if error occurred
: i2c-read1 ( slaveaddr -- c )
	i2c-7bitaddread i2c-sendaddr if $8000 exit then
	i2c-disableack
	i2c-clearaddr
	i2c-stop                            \ Stop I2C
	1000 i2c-settmo
	i2c-waitforrxne if $8000 exit then
	I2C1 _iDR @ $00FF and                \ Read the data from the DATA REGISTER
;

\ read multiple bytes from specifed address and stop
: _i2c-mread1 ( c-bufaddr -- n )
	1000 i2c-settmo
	i2c-waitforrxne if drop 0 exit then
	I2C1 _iDR @ swap c!                        \ Read the data and store in buffer
	1
;

: _i2c-mread2 ( c-bufaddr -- n )
	1000 i2c-settmo
	i2c-waitforrxbtf if drop 0 exit then
	i2c-stop
	I2C1 _iDR @ over c!                        \ Read the data and store in buffer
	1+
	I2C1 _iDR @ swap c!                        \ Read the data and store in buffer
	2
;

: _i2c-mread3 ( c-bufaddr -- n )
	1000 i2c-settmo
	i2c-waitforrxbtf if drop 0 exit then
	i2c-disableack
	I2C1 _iDR @ over c!                        \ Read the data and store in buffer
	1+
	i2c-waitforrxbtf if drop 0 exit then
	i2c-stop
	I2C1 _iDR @ over c!                        \ Read the data and store in buffer
	1+
	I2C1 _iDR @ swap c!                        \ Read the data and store in buffer
	3
;

: _i2c-mread4+ ( c-bufaddr -- n )
	1000 i2c-settmo
	i2c-waitforrxne if drop 0 exit then
	I2C1 _iDR @ over c!                        \ Read the data and store in buffer
	1+

	\ this is usually not the case
	%100 I2C1 _iSR1 bit@ if						\ if BTF set read another byte
		I2C1 _iDR @ swap c!                     \ Read the data and store in buffer
		2
	else
		drop
		1
	then
;

: _i2c_readbufpt2 ( xfersize c-bufaddr -- errflg )
	swap 	\ -- buf xfersize
	begin
		dup case
			0 of ." ERROR unexpected xfersize 0" 0 endof
			1 of over _i2c-mread1 endof
			2 of over _i2c-mread2 endof
			3 of over _i2c-mread3 endof
		 		 \ more than 3 -- buf xfersize n
	 			 drop over _i2c-mread4+
	 			 0 \ for endcase to drop
		endcase
			  		\ -- c-bufaddr xfersize n
		dup 0= if 2drop drop true exit then  \ if error detected
		            \ increment buffer address by number of bytes read
		rot over +  \ -- xfersize n newbuf
		-rot -    	\ decrement xfersize by number read -- buf xfersize
		dup 0=
	until
	2drop
	false
;

: i2c-readbuf ( n c-bufaddr slaveaddr -- errflg )
	2 pick 1 < if ." ERROR Must have at least 1 in buffer" 2drop drop true exit then
	i2c-7bitaddread i2c-sendaddr if 2drop true exit then

	over \ n addr n
	case
		0 of i2c-clearaddr i2c-stop endof    \ not used
		1 of i2c-disableack i2c-clearaddr i2c-stop endof
		2 of i2c-disableack	i2c-enablepos i2c-clearaddr endof
		\ >= 3
		i2c-enableack i2c-clearaddr
	endcase
				\ -- xfersize c-bufaddr
	_i2c_readbufpt2
;

\ for i2c eeprom access, very subtle differences to i2c-readbuf
: i2c-memory-read ( n c-bufaddr memaddr slaveaddr -- errflg )
	3 pick 1 < if ." ERROR Must have at least 1 in buffer" 2drop 2drop true exit then

    i2c-start if 2drop 2drop true exit then
    >r r@	\ save the chip address
	i2c-7bitaddwrite i2c-sendaddr if rdrop drop 2drop true exit then
	i2c-clearaddr
	\ send 16bit memory address (This is slightly different to using i2c-send1)
	1000 i2c-settmo
	i2c-waitfortxe if rdrop drop 2drop true exit then
	dup 8 rshift I2C1 _iDR !  					\ write MSB of memory address
	i2c-waitfortxe if rdrop drop 2drop true exit then
    $00FF and I2C1 _iDR ! 						\ write LSB of memory address
	i2c-waitfortxe if rdrop 2drop true exit then

	i2c-restart if rdrop 2drop true exit then
	r> i2c-7bitaddread i2c-sendaddr if 2drop true exit then

	over \ n addr n
	case
		1 of i2c-disableack i2c-clearaddr i2c-stop endof
		2 of i2c-disableack	i2c-enablepos i2c-clearaddr endof
		\ >= 3 (This is different to i2c-readbuf)
		i2c-clearaddr
	endcase
				\ -- xfersize c-bufaddr
	_i2c_readbufpt2
;

\ --------------------- Test stuff ------------------
\ print hex value n with x bits
: N#h. ( n bits -- )
	begin
		4 -
		2dup rshift $F and .digit emit
		dup 0=
	until 2drop
;

: 1#h. ( char -- )
	4 N#h.
;

: 2#h. ( char -- )
	8 N#h.
;

\ scan and report all I2C devices on the bus
: i2cScan. ( -- )
    i2c-init
    128 0 do
        cr i 2#h. ." :"
        16 0 do  space
          i j + i2c-deviceready? if i j + 2#h. else ." --" then
          \ i j + 2#h.
          2 ms
          key? if unloop unloop exit then
        loop
    16 +loop
    cr
;
