\ gpio-simple.fs

\ \ #include lib_registers.txt
\ \ #include lib_systick.txt

: setbit 1 swap lshift ;  \ Calculates the value for bit 0 (LSB) to bit 31 (MSB)

\ create a pin definition ( port pin -- addr ; -- pin port)
: pin <builds , , does> dup @ swap 1 cells + @ ;
\ eg PORTA 0 pin button1
\ eg PORTC 13 pin led1

\ set pin to output
: output ( pin port -- )
    MODE_OUTPUT -rot set-moder \ takes mode pin port
;

\ set output pin to opendrain
: opendrain ( pin port -- )
	>R %1 over lshift r@ _pOTYPER bic! 	\ clear ..
	%1 swap lshift R> _pOTYPER bis!		\ .. set opendrain
;

\ set pin to input
: input ( pin port -- )
    MODE_INPUT -rot set-moder \ takes mode pin port
;

\ set input pin to pull up
: pu ( pin port -- )
	>R 2* %11 over lshift r@ _pPUPDR bic! 	\ clear ..
	%01 swap lshift R> _pPUPDR bis!			\ .. set pullup
;

\ set input pin to pull down
: pd ( pinmsk port -- )
	>R 2* %11 over lshift r@ _pPUPDR bic! 	\ clear ..
	%10 swap lshift R> _pPUPDR bis!			\ .. set pulldown
;

\ set given pin to value
: set ( value pin port -- )
	_pODR
	swap bit swap
	rot         \ move value to tos
	0= if bic! else bis! then
;

\ is given pin set
: set? ( pin port -- ? )
	_pIDR swap bit swap bit@
;

\ define builtin pins
PORTC 13 pin led1
PORTA 0 pin switch1

0 variable cnt
: test
	init-delay
	led1 output

	switch1 2dup input pu

	begin
		$01 cnt bit@ led1 set

		200 ms

		1 cnt +!

		switch1 set? not if 0 cnt ! then

		key?
	until
;

PORTA 6 pin led2

: test2
	1 RCC _rAHB1ENR bis!					\ IO port A clock enabled GPIOAEN

	led2 output
	1 led2 set
	1000 ms

	begin
		$01 cnt bit@ led2 set
		200 ms
		1 cnt +!
	key? until
;
