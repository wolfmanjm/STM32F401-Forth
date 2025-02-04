\ \ #include i2c.fs
\ #include glcdfont.fs
#require i2c.fs
#require glcdfont.fs

\ -------------------------------------------------------------------------
\  Misc. helper words, constants, and variables
\ -------------------------------------------------------------------------


\ -------------------------------------------------------------------------
\  Drivers for OLED display with SSD1306 driver using I2C1.  The display
\  is treated as a character display with NUM_ROWS rows and NUM_COLS
\  columns.
\
\  There are two modes of use.  In both modes the display buffer
\  contents are sent to the display using the task
\
\                     ssd1306-show
\
\  This task is called in the main loop and writes one character
\  to the display per main loop tick (except for one initialization
\  tick).
\
\  In non-scrolling mode, strings are written to the display with a
\  specified starting row and column using the functions:
\
\                     ssd1306-wrt-str
\                     ssd1306-clr-str
\                     ssd1306-clr-row
\                     ssd1306-clr-scrn
\
\  In this mode, the display buffer is treated as non-circular for display
\  purposes, although writing a long string to it can wrap around to the
\  start.
\
\  In scrolling mode, strings are written at the display bottom row and
\  the rows above the bottom row are scrolled upward using the function:
\
\                     ssd1306-str-scroll
\
\  In this mode, the display buffer is treated as circular to facilitate
\  scrolling, with the display starting at the index value in the variable
\  ssd1306-Buf-Start.
\
\  The reset mode is non-scrolling with ssd1306-Buf-Start = 0, but if the
\  scrolling display function is used, the mode switches to scrolling.
\  To return to non-scrolling mode within a program, call the function:
\
\                     ssd1306-rst-scroll
\
\  which resets ssd1306-Buf-Start to zero and also clears the screen.
\

$3C constant SSD1306_I2C_ADDR   \ SSD1306 I2C address
128 constant SSD1306_WIDTH      \ SSD1306 display width in pixels
32 constant SSD1306_HEIGHT      \ SSD1306 LCD height in pixels

$00 constant SSD1306_BLACK
$01 constant SSD1306_WHITE

\ Commands
$81 constant SSD1306_SETCONTRAST
$A4 constant SSD1306_DISPLAYALLON_RESUME
$A5 constant SSD1306_DISPLAYALLON
$A6 constant SSD1306_NORMALDISPLAY
$A7 constant SSD1306_INVERTDISPLAY
$AE constant SSD1306_DISPLAYOFF
$AF constant SSD1306_DISPLAYON

$D3 constant SSD1306_SETDISPLAYOFFSET
$DA constant SSD1306_SETCOMPINS

$DB constant SSD1306_SETVCOMDETECT

$D5 constant SSD1306_SETDISPLAYCLOCKDIV
$D9 constant SSD1306_SETPRECHARGE

$A8 constant SSD1306_SETMULTIPLEX

$00 constant SSD1306_SETLOWCOLUMN
$10 constant SSD1306_SETHIGHCOLUMN

$40 constant SSD1306_SETSTARTLINE

$20 constant SSD1306_MEMORYMODE
$21 constant SSD1306_COLUMNADDR
$22 constant SSD1306_PAGEADDR

$C0 constant SSD1306_COMSCANINC
$C8 constant SSD1306_COMSCANDEC

$A0 constant SSD1306_SEGREMAP

$8D constant SSD1306_CHARGEPUMP

$01 constant SSD1306_EXTERNALVCC
$02 constant SSD1306_SWITCHCAPVCC


$40 constant SSD1306_DC_BIT_DATA
$00 constant SSD1306_DC_BIT_CMD

\ Character row and column sizes
4 constant SSD1306_NUM_ROWS
21 constant SSD1306_NUM_COLS
84 constant SSD1306_BUF_LGT

: ssd1306-write ( n buf -- err )
	SSD1306_I2C_ADDR i2c-sendbufstop
;

\ -------------------------------------------------------------------------

\ Write a command to the SSD1306 display controller
: ssd1306-wrtcmd ( cmd-byte -- )
  SSD1306_DC_BIT_CMD SSD1306_I2C_ADDR i2c-send2stop
  if cr ." Problem writing SSD1306 command" then
;

5 buffer: ssd1306-Fill-Buf
\ Fill display with a single color
: ssd1306-fill-scrn ( color-byte -- )
  SSD1306_WHITE = if $FF else $00 then
  SSD1306_DC_BIT_DATA ssd1306-Fill-Buf c!
  dup ssd1306-Fill-Buf 1 + c!
  dup ssd1306-Fill-Buf 2 + c!
  dup ssd1306-Fill-Buf 3 + c!
  ssd1306-Fill-Buf 4 + c!

  4 0 do
    $B0 i + ssd1306-wrtcmd      \ Set row (ssd1306 page)
    \ Clear four columns at a time in one row
    SSD1306_WIDTH 2 rshift 0 do
      SSD1306_SETLOWCOLUMN i 2 lshift $F and + ssd1306-wrtcmd
      SSD1306_SETHIGHCOLUMN i 2 rshift $F and + ssd1306-wrtcmd
      5 ssd1306-Fill-Buf ssd1306-write
      if cr ." Problem filling SSD1306" unloop unloop exit then
    loop
  loop
;

7 buffer: ssd1306-Chr-Buf
\ Draw a single character on the display at row and col position
\ Note:  Row and col value limits are not checked here, as this is a
\        helper word.  Values must satisfy 0 <= row < SSD1306_NUM_ROWS,
\        0 <= col < SSD1306_NUM_COLS.
: ssd1306-draw-chr ( row col char -- )

  \ Load character bit pattern into i2c buffer (including one trailing
  \ blank column) to send as 6 data bytes
  SSD1306_DC_BIT_DATA ssd1306-Chr-Buf c!
  6 0 do
    dup 5x7FONT swap 6 * i + + c@ ssd1306-Chr-Buf i + 1+ c!
  loop
  drop    \ Drop char from stack

  swap $B0 + ssd1306-wrtcmd   \ Set row (ssd1306 page), row consumed
  \ Set ssd1306 columns
  6 * 1 + dup
  $F and SSD1306_SETLOWCOLUMN + ssd1306-wrtcmd
  4 rshift $F and SSD1306_SETHIGHCOLUMN + ssd1306-wrtcmd

  7 ssd1306-Chr-Buf ssd1306-write
  if cr ." Problem writing character to SSD1306" then

;

SSD1306_BUF_LGT buffer: ssd1306-Txt-Buf
\ Starting index to display, treating ssd1306-Text-Buf as circular
0 variable ssd1306-Buf-Start

0 constant SSD1306_SHOW_START
1 constant SSD1306_SHOW_DRAW
0 variable ssd1306-Show-State

0 variable ssd1306-Show-Row
0 variable ssd1306-Show-Col

\ Show characters in the text buffer on the display.
\ This task is called in the main loop and writes one character
\ to the display per main loop tick (except for one initialization
\ tick), going through all characters in the text buffer.
\ The variable ssd1306-Buf-Start is controlled by the write functions.
\ In non-scrolling mode it will remain zero.  In scrolling mode its value
\ will change to scroll the rows upward in the display.
: ssd1306-show ( -- )
  ssd1306-Show-State @ case

    SSD1306_SHOW_START of
      0 ssd1306-Show-Row !
      0 ssd1306-Show-Col !
      SSD1306_SHOW_DRAW ssd1306-Show-State !
    endof

    SSD1306_SHOW_DRAW of
      ssd1306-Show-Row @
      ssd1306-Show-Col @
      over SSD1306_NUM_COLS * over + ssd1306-Buf-Start @ +
      SSD1306_BUF_LGT mod ssd1306-Txt-Buf + c@
      ssd1306-draw-chr

      1 ssd1306-Show-Col +!
      ssd1306-Show-Col @
      SSD1306_NUM_COLS >= if
        0 ssd1306-Show-Col !
        1 ssd1306-Show-Row +!
        ssd1306-Show-Row @
        SSD1306_NUM_ROWS >= if
          SSD1306_SHOW_START ssd1306-Show-State !
        then
      then
    endof

  endcase
;

\ -------------------------------------------------------------------------
\ These tasks are for non-scrolling mode only.

\ Write a string to the display buffer starting at row and col position.
\ Text is wrapped if it exceeds buffer size.
: ssd1306-wrt-str ( row col str-addr str-len  -- )
  \ Check limits 0 <= row < SSD1306_NUM_ROWS
  3 pick dup 0< swap SSD1306_NUM_ROWS 1 - > or if
    drop drop drop drop exit
  then
  \ Check limits 0 <= col < SSD1306_NUM_COLS
  2 pick dup 0< swap SSD1306_NUM_COLS 1 - > or if
    drop drop drop drop exit
  then

  \ Calculate starting index in text buffer
  3 pick SSD1306_NUM_COLS * 3 pick + -rot

  0 do
    \ ( stack: row col start str-addr )
    dup i + c@ 2 pick i + SSD1306_BUF_LGT mod
    ssd1306-Txt-Buf + c!
  loop
  drop drop drop drop
;

\ Clear cnt characters of text starting at row and col posittion.
\ Must have 0<=row<=3 and 0<=col<=21.  Wraps in the text buffer.
: ssd1306-clr-str ( row col cnt -- )
  \ Check limits 0 <= row < SSD1306_NUM_ROWS
  2 pick dup 0< swap SSD1306_NUM_ROWS 1 - > or if
    drop drop drop exit
  then
  \ Check limits 0 <= col < SSD1306_NUM_COLS
  over dup 0< swap SSD1306_NUM_COLS 1 - > or if
    drop drop drop exit
  then

  \ Calculate starting index in text buffer
  2 pick SSD1306_NUM_COLS * 2 pick + swap

  0 do
    \ ( stack: row col start )
    dup i + SSD1306_BUF_LGT mod
    ssd1306-Txt-Buf + 32 swap c!    \ Load space
  loop
  drop drop drop
;

: ssd1306-clr-row ( row -- )
  \ Check limits 0 <= row < SSD1306_NUM_ROWS
  dup dup 0< swap SSD1306_NUM_ROWS 1 - > or if
    drop exit
  then

  0 SSD1306_NUM_COLS ssd1306-clr-str
;

: ssd1306-clr-scrn ( -- )
  0 0 SSD1306_BUF_LGT ssd1306-clr-str
;

\ -------------------------------------------------------------------------
\ These tasks are for scrolling mode

\ Write text to the bottom line of the display, shifting the other rows
\ upward.  If the text is shorter than one row, trailing blanks are
\ added.  If the text is longer than one row, rows are added at the
\ bottom until all the text is written (with trailing blanks on the
\ last row, if needed).  If the text is longer than all rows in the
\ display, then only the last group of characters that fits in the
\ display will be shown.
: ssd1306-str-scroll ( str-addr str-lgt -- )
  \ Exit if string length non-positive
  dup 0 <= if
    drop exit
  then
  \ Prepare to over-write previous first row with new bottom row
  ssd1306-Buf-Start @ dup
  \ ( stack: str-addr str-lgt new-row-start old-start)
  \ Make previously second row now the first row
  SSD1306_NUM_COLS + SSD1306_BUF_LGT mod ssd1306-Buf-Start !
  \ ( stack: str-addr str-lgt new-row-start)
  \ Write out new bottom row, with trailing blanks, if needed
  SSD1306_NUM_COLS 0 do
    dup i + SSD1306_BUF_LGT mod ssd1306-Txt-Buf +
    \ ( stack: str-addr str-lgt new-row-start store-addr)
    2 pick i > if
      3 pick i + c@ swap c!   \ i < str-lgt, write next char from str
    else
      32 swap c!               \ i >= str-lgt, write space
    then
  loop
  drop drop drop
;

\ Switch back to non-scrolling mode and clear the display
: ssd1306-rst-scroll ( -- )
  0 ssd1306-Buf-Start !
  ssd1306-clr-scrn
;

\ -------------------------------------------------------------------------
\ Initialization tasks
: ssd1306-init-hw

  SSD1306_DISPLAYOFF ssd1306-wrtcmd

  SSD1306_MEMORYMODE ssd1306-wrtcmd
  $02 ssd1306-wrtcmd    \ Page address mode

  SSD1306_SETCONTRAST ssd1306-wrtcmd
  $F0 ssd1306-wrtcmd

  SSD1306_NORMALDISPLAY ssd1306-wrtcmd

  SSD1306_SETMULTIPLEX ssd1306-wrtcmd
  SSD1306_HEIGHT 1 - ssd1306-wrtcmd

  SSD1306_DISPLAYALLON_RESUME ssd1306-wrtcmd

  SSD1306_SETDISPLAYOFFSET ssd1306-wrtcmd
  $00 ssd1306-wrtcmd

  SSD1306_SETDISPLAYCLOCKDIV ssd1306-wrtcmd
  $80 ssd1306-wrtcmd

  SSD1306_SETPRECHARGE ssd1306-wrtcmd
  $22 ssd1306-wrtcmd

  SSD1306_SETCOMPINS ssd1306-wrtcmd
  $02 ssd1306-wrtcmd    \ 128*32

  SSD1306_SETVCOMDETECT ssd1306-wrtcmd
  $40 ssd1306-wrtcmd

  SSD1306_CHARGEPUMP ssd1306-wrtcmd
  $14 ssd1306-wrtcmd

  SSD1306_DISPLAYON ssd1306-wrtcmd
;

: ssd1306-init ( -- errflg )
  SSD1306_I2C_ADDR i2c-deviceready? not if ." ssd1306 not found" true then

  ssd1306-init-hw  \ Initialize SSD1306 display controller

  SSD1306_BLACK ssd1306-fill-scrn
  0 ssd1306-Buf-Start !     \ Non-scrolling mode
  ssd1306-clr-scrn
  false
;

: n>str s>d TUCK DABS <#  #S ROT SIGN  #> ;
: lcd-printn ( n -- ) n>str ssd1306-wrt-str ;
: display SSD1306_BUF_LGT 1 + 0 do ssd1306-show loop ;


0 variable tcnt
: ssd1306-splash1 s" *********************" ;
: ssd1306-splash2 s" *    Test           *" ;

: test-lcd
  i2c-init
  ssd1306-init if exit then

  0 0 ssd1306-splash1 ssd1306-wrt-str
  1 0 ssd1306-splash2 ssd1306-wrt-str
  2 0 ssd1306-splash1 ssd1306-wrt-str

  begin
    1 tcnt +!
    3 ssd1306-clr-row
    3 0 s" count: " ssd1306-wrt-str
    3 7 tcnt @ lcd-printn
    display
    200 ms
  key? until
;

16 buffer: test-chars
: _test-display-chars ( ch -- )
	>r
	ssd1306-clr-scrn
	r@ 16 0 do dup      i + test-chars i + c! loop 0 2 test-chars 16 ssd1306-wrt-str drop
	r@ 16 0 do dup 16 + i + test-chars i + c! loop 1 2 test-chars 16 ssd1306-wrt-str drop
	r@ 16 0 do dup 32 + i + test-chars i + c! loop 2 2 test-chars 16 ssd1306-wrt-str drop
	r@ 16 0 do dup 48 + i + test-chars i + c! loop 3 2 test-chars 16 ssd1306-wrt-str drop
	rdrop
	display
	2000 ms
;

: test-fonts
  	i2c-init
  	ssd1306-init if exit then
  	begin
  		." 0..63" cr
  		0   _test-display-chars
  		." 64..127" cr
  		64  _test-display-chars
  		." 128..191" cr
  		128 _test-display-chars
  		." 192..255" cr
  		192 _test-display-chars
  	key? until
;
