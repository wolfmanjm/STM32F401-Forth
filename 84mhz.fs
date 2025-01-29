\ set to 84mhz

\ #include lib_registers.txt
\ #include lib_systick.txt

16 bit constant HSEON
17 bit constant HSERDY
24 bit constant PLLON
25 bit constant PLLRDY
$40023C00 constant FLASH_ACR 					\ Flash Access Control Register
8 bit 9 bit 10 bit or or 2 + constant ART-2WS 	\ Enable data cache, instruction and prefetching, 2 wait states

2 28 lshift       \ reserved
7 24 lshift or    \ PLLQ = 7
22 bit or         \ PLLSRC = HSE
%01 16 lshift or  \ PLLP = 4
336 6 lshift or   \ PLLN = 336
25 +              \ PLLM = 25
constant pll-16>84

\ other divs not divided
%100 10 lshift  \ APB1 DIV 2
constant cfg-pll

$40007000 constant PWR     ( Power control )
PWR $0 +  constant PWR_CR  ( read-write    ) \ power control register
PWR $4 +  constant PWR_CSR (               ) \ power control/status register

: hse-on ( -- ) HSEON RCC _rCR bis! begin HSERDY RCC _rCR bit@ until ;
: pll-on ( -- ) PLLON RCC _rCR bis! begin PLLRDY RCC _rCR bit@ until ;
: pll-84MHz ( -- ) pll-16>84 RCC _rPLLCFGR ! ;
: flash-84MHz ( -- ) ART-2WS FLASH_ACR ! ;

: set-divs ( -- )
	%111 13 lshift     \ PPRE2
	%111 10 lshift or  \ PPRE1
	%1111 4 lshift or  \ HPRE
	%11            or  \ SW
	RCC _rCFGR bic!  \ clear dividers and select HSI

	cfg-pll RCC _rCFGR bis! \ set dividers APB1 DIV 2
;

: use-pll ( -- )
	%11 RCC _rCFGR bic!  \ clear bits
	%10 RCC _rCFGR bis!  \ set PLL select
	begin %1000 RCC _rCFGR bit@ until
;

: set-baud ( -- ) baud USART2 _uBRR ! ;
: 84MHz ( -- )
	1 28 lshift RCC _rAHB1ENR bis!  \ enable pwr
	  RCC _rAHB1ENR @ drop
	%11 14 lshift PWR_CR bic!   \ clear scale
	%10 14 lshift PWR_CR bis!   \ set scale2
	  PWR_CR @ drop
	hse-on
	flash-84MHz
	pll-84MHz
	pll-on
	set-divs
	use-pll
	84000000 2 / hclk ! 115200 set-baud \ usart2 uses APB1 which is hclk/2
	84000000 hclk !
	init-Systimer
;

16000000 constant HSI_VALUE
25000000 constant HSE_VALUE
3 2 lshift constant RCC_CFGR_SWS
1 22 lshift constant RCC_PLLCFGR_PLLSRC
$3F 0 lshift constant RCC_PLLCFGR_PLLM
$1FF 6 lshift constant RCC_PLLCFGR_PLLN
3 16 lshift constant RCC_PLLCFGR_PLLP
$f 4 lshift constant RCC_CFGR_HPRE

: get-pll
	RCC _rPLLCFGR @ RCC_PLLCFGR_PLLSRC and 22 rshift \ pllsource
	RCC _rPLLCFGR @ RCC_PLLCFGR_PLLM and             \ pllm
	." PLLM: " dup .
	swap 0<> if \ pllsource != 0 HSE used as PLL clock source
		HSE_VALUE
		." HSE: " dup .
	else        \ HSI used as PLL clock source
		HSI_VALUE
		." HSI: " dup .
	then

	swap / RCC _rPLLCFGR @ RCC_PLLCFGR_PLLN and 6 rshift	\ pllvco
	." PLLN: " dup .
	*
	RCC _rPLLCFGR @ RCC_PLLCFGR_PLLP and 16 rshift 1+ 2*      \ pllp
	." PLLP: " dup .
	/
;

: get-clk-src RCC _rCFGR @ RCC_CFGR_SWS and ;

: get_system_core_clock
	get-clk-src
	case
		0 of HSI_VALUE endof
		4 of HSE_VALUE endof
		8 of get-pll endof
	endcase
;

: get_hclk
	RCC _rCFGR @ RCC_CFGR_HPRE and 4 rshift
	dup 8 < if drop get_system_core_clock
		else
		dup 12 < if 7 - else 6 - then
			get_system_core_clock swap rshift
	then
;
