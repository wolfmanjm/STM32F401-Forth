\ quadrature encoder driver
\ on PA0 and PA1

\ TODO move to lib_registers
: set-pupdn ( mode pin port -- )
    >R 2* %11 over lshift r@ _pPUPDR bic! \ clear ..
    lshift R> _pPUPDR bis!                \ .. set
;

: qei-init
    1 RCC _rAHB1ENR bis!                    \ Enable clock for GPIOA
    MODE_Alternate 0 PORTA set-moder        \ PA0 & PA1 as Alternate Function
    MODE_Alternate 1 PORTA set-moder        \ PA0 & PA1 as Alternate Function
    SPEED_LOW 0 PORTA set-opspeed
    SPEED_LOW 1 PORTA set-opspeed           \ set speed
    %01 0 PORTA set-pupdn                   \ set to pullup
    %01 1 PORTA set-pupdn                   \ set to pullup

    1 0 PORTA set-alternate                 \ set AF01
    1 1 PORTA set-alternate

    \ configure TIM2 as Encoder input
    1 RCC _rAPB1ENR bis!                   \ Enable clock for TIM2

    $0001     TIM2 _tCR1   !               \  CEN(Counter ENable)='1'     < TIM control register 1
    $0003     TIM2 _tSMCR  !               \  SMS='011' (Encoder mode 3)  < TIM slave mode control register
    $F1F1     TIM2 _tCCMR1 !               \  CC1S='01' CC2S='01'         < TIM capture/compare mode register 1
    $0000     TIM2 _tCCMR2 !               \                              < TIM capture/compare mode register 2
    $0011     TIM2 _tCCER  !               \  CC1P CC2P                   < TIM capture/compare enable register
    $0000     TIM2 _tPSC   !               \  Prescaler = (0+1)           < TIM prescaler
    $ffffffff TIM2 _tARR   !               \  reload at 0xfffffff         < TIM auto-reload register
    0 TIM2 _tCNT !                         \ reset the counter before we use it
;

: qei-get ( -- cnt )
    TIM2 _tCNT @
;

: test-qei
    qei-init
    qei-get
    begin
        qei-get \ Get current position from Encoder
        dup 2 roll <> if dup 2/ 2/ . cr then
        100 ms
    key? until
;
