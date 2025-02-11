\ SPI1 PB3 - SCK, PB4 - MISO, PB5 - MOSI at AF05

\ helpers

\ sets or clears a range of bits in addr
: bits! ( value mask pos addr -- )
    >r tuck         \ -- value pos mask pos
    lshift r@ bic!  \ clear mask first
    lshift r> bis!  \ set the value bits
;

$40013000 constant SPI1

\ cpol cpha
$00 constant SPI_MODE0
$01 constant SPI_MODE1
$10 constant SPI_MODE2
$11 constant SPI_MODE3

: spi-init
    \ Enable SPI1 clock (on APB2)
    1 12 lshift RCC _rAPB2ENR bis!   \ Set SPI1EN bit
    \ Enable GPIOB clock
    2 RCC _rAHB1ENR bis!            \ Enable GPIOB clock

    \ Configure PB3 (SCK), PB4 (MISO), PB5 (MOSI)
    MODE_Alternate 3 PORTB set-moder
    MODE_Alternate 4 PORTB set-moder
    MODE_Alternate 5 PORTB set-moder

    \ Select AF5 (SPI1) for these pins
    5 3 PORTB set-alternate
    5 4 PORTB set-alternate
    5 5 PORTB set-alternate

    \ Set GPIO pins to High-speed mode
    SPEED_HIGH 3 PORTB set-opspeed
    SPEED_HIGH 4 PORTB set-opspeed
    SPEED_HIGH 5 PORTB set-opspeed

    \ Set push-pull mode and no pull-up/pull-down
    1 3 lshift 1 4 lshift or 1 5 lshift or PORTB _pOTYPER bic!
    1 3 lshift 1 4 lshift or 1 5 lshift or PORTB _pPUPDR bic!

    \ Disable SPI before configuring and set everything to 0
    0 SPI1 _sCR1 !
    \ Set Master mode
    1 2 lshift SPI1 _sCR1 bis!
    \ Set Baud rate (fPCLK/32) -> BR[2:0] = 0b100 @ 84MHz = 2.625MHz
    %100 3 lshift SPI1 _sCR1 bis!
    \ Set Clock Polarity (CPOL = 0) and Clock Phase (CPHA = 0)
    %11 SPI1 _sCR1 bic!
    \ Set Data Frame Format to 8-bit (DFF = 0)
    1 11 lshift SPI1 _sCR1 bic!
    \ Set MSB First (LSBFIRST = 0)
    1 7 lshift SPI1 _sCR1 bic!

    \ Set Software Slave Management (SSM = 1), Internal Slave Select (SSI = 1)
    1 9 lshift 1 8 lshift or SPI1 _sCR1 bis!
    \ Enable SPI Peripheral
    1 6 lshift SPI1 _sCR1 bis!
;

: spi-mode ( mode -- )
    \ Set Clock Polarity (CPOL) and Clock Phase (CPHA)
    %11 and
    %11 0 SPI1 _sCR1 bits!
;

\ find the closest baud rate that is slower or equal to the requested one
: spi-baud ( baud -- )
    8 0 do
        hclk @ 1 i 1+ lshift / over
        u<= if
            i %111 3 SPI1 _sCR1 bits!
            unloop drop exit
        then
    loop
    \ use slowest
    %111 %111 3 SPI1 _sCR1 bits!
    drop
;

: spi-busy? ( -- flg )
    %10000000 SPI1 _sSR bit@        \ check Busy
;

: spi-waitbusy
    begin spi-busy? not until
;

: spi-enable ( flg -- )
    %1000000 SPI1 _sCR1 rot if bis! else bic! then
;

\ NOTE all the following need to be preceded by enabling the relevant chip select
\ and a wait on the busy flag at the end before deasserting the chip select
: _spi! ( data -- )
    begin %10 SPI1 _sSR bit@ until \ Wait for TXE
    SPI1 _sDR !                    \ Send data
;

: _spi@ ( -- data )
    begin %1 SPI1 _sSR bit@ until   \ Wait for RXNE
    SPI1 _sDR @                     \ Read received data
;

: spi!@ ( txdata -- rxdata )
    _spi! _spi@
;

: spi-write1 ( data -- )
    spi!@ drop
;

: spi-read1 ( -- data )
    0 spi!@
;

\ stack based i/o
: >spi ( d1 d2 d3 ... dn n -- )
    0 do spi-write1 loop
;

: spi> ( n .. d1 d2 d3 ... dn )
    0 do spi-read1 loop
;

