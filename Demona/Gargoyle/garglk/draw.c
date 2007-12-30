#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>
#include "glk.h"
#include "garglk.h"

void gli_get_builtin_font(int idx, unsigned char **ptr, unsigned int *len);

#include <ft2build.h>
#include FT_FREETYPE_H

#include <math.h> /* for pow() */

#define mul255(a,b) (((a) * ((b) + 1)) >> 8)

#ifdef _WIN32
#define inline	__inline
#endif

typedef struct font_s font_t;
typedef struct bitmap_s bitmap_t;

struct bitmap_s
{
    int w, h, lsb, top, pitch;
    unsigned char *data;
};

struct font_s
{
    FT_Face face;
	char loaded[256];
    int advs[256];
    bitmap_t glyphs[256][GLI_SUBPIX];
};

/*
 * Globals
 */

static unsigned char gammamap[256];

static font_t gfont_table[8];

int gli_cellw = 8;
int gli_cellh = 8;

int gli_image_s = 0;
int gli_image_w = 0;
int gli_image_h = 0;
unsigned char *gli_image_rgb = NULL;

static FT_Library ftlib;

/*
 * Font loading
 */

static int touni(int enc)
{
	switch (enc)
	{
		case LIG_FI: return 0xFB01;
		case LIG_FL: return 0xFB02;
		case UNI_LSQUO: return 0x2018;
		case UNI_RSQUO: return 0x2019;
		case UNI_LDQUO: return 0x201c;
		case UNI_RDQUO: return 0x201d;
		case UNI_NDASH: return 0x2013;
		case UNI_MDASH: return 0x2014;
	}
	return enc;
}

static void gammacopy(unsigned char *dst, unsigned char *src, int n)
{
    while (n--)
        *dst++ = gammamap[*src++];
}

#define m28(x) ((x * 28) / 255)
#define m56(x) ((x * 56) / 255)
#define m85(x) ((x * 85) / 255)

static void gammacopy_lcd(unsigned char *dst, unsigned char *src, int w, int h, int pitch)
{
    const unsigned char zero[3] = { 0, 0, 0 };
    unsigned char *dp, *sp;
    int x, y;

    for (y = 0; y < h; y++)
    {
        sp = &src[y * pitch];
        dp = &dst[y * pitch];
        for (x = 0; x < w; x += 3)
        {
            const unsigned char *lf = x > 0 ? sp - 3 : zero;
            const unsigned char *rt = x < w - 3 ? sp + 3 : zero;
            unsigned char ct[3];
            ct[0] = gammamap[sp[0]];
            ct[1] = gammamap[sp[1]];
            ct[2] = gammamap[sp[2]];
            dp[0] = m28(lf[1]) + m56(lf[2]) + m85(ct[0]) + m56(ct[1]) + m28(ct[2]);
            dp[1] = m28(lf[2]) + m56(ct[0]) + m85(ct[1]) + m56(ct[2]) + m28(rt[0]);
            dp[2] = m28(ct[0]) + m56(ct[1]) + m85(ct[2]) + m56(rt[0]) + m28(rt[1]);
            sp += 3;
            dp += 3;
        }
    }
}

static void loadglyph(font_t *f, int enc)
{
	FT_Vector v;
	int err;
	int cid;
	int gid;
	int x;

	f->loaded[enc] = 1;

	cid = touni(enc);

	gid = FT_Get_Char_Index(f->face, cid);
	if (gid <= 0)
		gid = FT_Get_Char_Index(f->face, '?');

	for (x = 0; x < GLI_SUBPIX; x++)
	{
		v.x = (x * 64) / GLI_SUBPIX;
		v.y = 0;

		FT_Set_Transform(f->face, 0, &v);

		err = FT_Load_Glyph(f->face, gid,
				FT_LOAD_NO_BITMAP | FT_LOAD_NO_HINTING);
		if (err)
			winabort("FT_Load_Glyph");

                if (gli_conf_lcd)
                    err = FT_Render_Glyph(f->face->glyph, FT_RENDER_MODE_LCD);
                else
                    err = FT_Render_Glyph(f->face->glyph, FT_RENDER_MODE_LIGHT);
		if (err)
			winabort("FT_Render_Glyph");

		f->advs[enc] = (f->face->glyph->advance.x * GLI_SUBPIX + 32) / 64;

		f->glyphs[enc][x].lsb = f->face->glyph->bitmap_left;
		f->glyphs[enc][x].top = f->face->glyph->bitmap_top;
		f->glyphs[enc][x].w = f->face->glyph->bitmap.width;
		f->glyphs[enc][x].h = f->face->glyph->bitmap.rows;
		f->glyphs[enc][x].pitch = f->face->glyph->bitmap.pitch;
		f->glyphs[enc][x].data =
			malloc(f->glyphs[enc][x].pitch * f->glyphs[enc][x].h);
                if (gli_conf_lcd)
                    gammacopy_lcd(f->glyphs[enc][x].data,
                            f->face->glyph->bitmap.buffer,
                            f->glyphs[enc][x].w, f->glyphs[enc][x].h, f->glyphs[enc][x].pitch);
                else
                    gammacopy(f->glyphs[enc][x].data,
                            f->face->glyph->bitmap.buffer,
                            f->glyphs[enc][x].pitch * f->glyphs[enc][x].h);
        }
}

static void loadfont(font_t *f, char *name, float size, float aspect)
{
	static char *map[8] =
	{
		"LuxiMonoRegular",
		"LuxiMonoBold",
		"LuxiMonoOblique",
		"LuxiMonoBoldOblique",
		"CharterBT-Roman",
		"CharterBT-Bold",
		"CharterBT-Italic",
		"CharterBT-BoldItalic",
	};

	char afmbuf[1024];
	unsigned char *mem;
	unsigned int len;
	int err = 0;
	int i;

	memset(f, 0, sizeof (font_t));

	for (i = 0; i < 8; i++)
	{
		if (!strcmp(name, map[i]))
		{
			gli_get_builtin_font(i, &mem, &len);
			err = FT_New_Memory_Face(ftlib, mem, len, 0, &f->face);
			if (err)
				winabort("FT_New_Face: %s: 0x%x", name, err);
			break;
		}
	}
	if (i == 8)
	{
		err = FT_New_Face(ftlib, name, 0, &f->face);
		if (err)
			winabort("FT_New_Face: %s: 0x%x", name, err);
		if (strstr(name, ".PFB") || strstr(name, ".PFA") ||
				strstr(name, ".pfb") || strstr(name, ".pfa"))
		{
			strcpy(afmbuf, name);
			strcpy(strrchr(afmbuf, '.'), ".afm");
			FT_Attach_File(f->face, afmbuf);
			strcpy(afmbuf, name);
			strcpy(strrchr(afmbuf, '.'), ".AFM");
			FT_Attach_File(f->face, afmbuf);
		}
	}

	err = FT_Set_Char_Size(f->face, size * aspect * 64, size * 64, 72, 72);
	if (err)
		winabort("FT_Set_Char_Size: %s", name);

	err = FT_Select_Charmap(f->face, ft_encoding_unicode);
	if (err)
		winabort("FT_Select_CharMap: %s", name);

	for (i = 0; i < 256; i++)
		f->loaded[i] = 0;
}

#if 0
	for (i = 32; i < 128; i++)
		loadglyph(f, i, i);
	for (i = 160; i < 256; i++)
		loadglyph(f, i, i);
	loadglyph(f, LIG_FI, touni(LIG_FI));
	loadglyph(f, LIG_FL, touni(LIG_FL));
	loadglyph(f, UNI_LSQUO, touni(UNI_LSQUO));
	loadglyph(f, UNI_RSQUO, touni(UNI_RSQUO));
	loadglyph(f, UNI_LDQUO, touni(UNI_LDQUO));
	loadglyph(f, UNI_RDQUO, touni(UNI_RDQUO));
	loadglyph(f, UNI_NDASH, touni(UNI_NDASH));
	loadglyph(f, UNI_MDASH, touni(UNI_MDASH));
#endif

void gli_initialize_fonts(void)
{
	float monoaspect = gli_conf_monoaspect;
	float propaspect = gli_conf_propaspect;
	float monosize = gli_conf_monosize;
	float propsize = gli_conf_propsize;
	int err;
	int i;

	for (i = 0; i < 256; i++)
		gammamap[i] = pow(i / 255.0, gli_conf_gamma) * 255.0;

	err = FT_Init_FreeType(&ftlib);
	if (err)
		winabort("FT_Init_FreeType");

	loadfont(&gfont_table[0], gli_conf_monor, monosize, monoaspect);
	loadfont(&gfont_table[1], gli_conf_monob, monosize, monoaspect);
	loadfont(&gfont_table[2], gli_conf_monoi, monosize, monoaspect);
	loadfont(&gfont_table[3], gli_conf_monoz, monosize, monoaspect);

	loadfont(&gfont_table[4], gli_conf_propr, propsize, propaspect);
	loadfont(&gfont_table[5], gli_conf_propb, propsize, propaspect);
	loadfont(&gfont_table[6], gli_conf_propi, propsize, propaspect);
	loadfont(&gfont_table[7], gli_conf_propz, propsize, propaspect);

	loadglyph(&gfont_table[0], '0');

	gli_cellh = gli_leading;
	gli_cellw = (gfont_table[0].advs['0'] + GLI_SUBPIX - 1) / GLI_SUBPIX;
}

/*
 * Drawing
 */

void gli_draw_pixel(int x, int y, unsigned char alpha, unsigned char *rgb)
{
	unsigned char *p = gli_image_rgb + y * gli_image_s + x * 3;
	unsigned char invalf = 255 - alpha;
	if (x < 0 || x >= gli_image_w)
		return;
	if (y < 0 || y >= gli_image_h)
		return;
#ifdef WIN32
	p[0] = rgb[2] + mul255((short)p[0] - rgb[2], invalf);
	p[1] = rgb[1] + mul255((short)p[1] - rgb[1], invalf);
	p[2] = rgb[0] + mul255((short)p[2] - rgb[0], invalf);
#else
	p[0] = rgb[0] + mul255((short)p[0] - rgb[0], invalf);
	p[1] = rgb[1] + mul255((short)p[1] - rgb[1], invalf);
	p[2] = rgb[2] + mul255((short)p[2] - rgb[2], invalf);
#endif
}

void gli_draw_pixel_lcd(int x, int y, unsigned char *alpha, unsigned char *rgb)
{
	unsigned char *p = gli_image_rgb + y * gli_image_s + x * 3;
	unsigned char invalf[3];
        invalf[0] = 255 - alpha[0];
        invalf[1] = 255 - alpha[1];
        invalf[2] = 255 - alpha[2];
	if (x < 0 || x >= gli_image_w)
		return;
	if (y < 0 || y >= gli_image_h)
		return;
#ifdef WIN32
	p[0] = rgb[2] + mul255((short)p[0] - rgb[2], invalf[2]);
	p[1] = rgb[1] + mul255((short)p[1] - rgb[1], invalf[1]);
	p[2] = rgb[0] + mul255((short)p[2] - rgb[0], invalf[0]);
#else
	p[0] = rgb[0] + mul255((short)p[0] - rgb[0], invalf[0]);
	p[1] = rgb[1] + mul255((short)p[1] - rgb[1], invalf[1]);
	p[2] = rgb[2] + mul255((short)p[2] - rgb[2], invalf[2]);
#endif
}


static inline void draw_bitmap(bitmap_t *b, int x, int y, unsigned char *rgb)
{
	int i, k, c;
	for (k = 0; k < b->h; k++)
	{
		for (i = 0; i < b->w; i ++)
		{
			c = b->data[k * b->pitch + i];
			gli_draw_pixel(x + b->lsb + i, y - b->top + k, c, rgb);
		}
	}
}

static inline void draw_bitmap_lcd(bitmap_t *b, int x, int y, unsigned char *rgb)
{
    int i, j, k;
    for (k = 0; k < b->h; k++)
    {
        for (i = 0, j = 0; i < b->w; i += 3, j ++)
        {
            gli_draw_pixel_lcd(x + b->lsb + j, y - b->top + k, b->data + k * b->pitch + i, rgb);
        }
    }
}

void gli_draw_clear(unsigned char *rgb)
{
	unsigned char *p;
	int x, y;

	for (y = 0; y < gli_image_h; y++)
	{
		p = gli_image_rgb + y * gli_image_s;
		for (x = 0; x < gli_image_w; x++)
		{
#ifdef WIN32
			*p++ = rgb[2];
			*p++ = rgb[1];
			*p++ = rgb[0];
#else
			*p++ = rgb[0];
			*p++ = rgb[1];
			*p++ = rgb[2];
#endif
		}
	}
}

void gli_draw_rect(int x0, int y0, int w, int h, unsigned char *rgb)
{
	unsigned char *p0;
	int x1 = x0 + w;
	int y1 = y0 + h;
	int x, y;

	if (x0 < 0) x0 = 0;
	if (y0 < 0) y0 = 0;
	if (x1 < 0) x1 = 0;
	if (y1 < 0) y1 = 0;

	if (x0 > gli_image_w) x0 = gli_image_w;
	if (y0 > gli_image_h) y0 = gli_image_h;
	if (x1 > gli_image_w) x1 = gli_image_w;
	if (y1 > gli_image_h) y1 = gli_image_h;

	p0 = gli_image_rgb + y0 * gli_image_s + x0 * 3;

	for (y = y0; y < y1; y++)
	{
		unsigned char *p = p0;
		for (x = x0; x < x1; x++)
		{
#ifdef WIN32
			*p++ = rgb[2];
			*p++ = rgb[1];
			*p++ = rgb[0];
#else
			*p++ = rgb[0];
			*p++ = rgb[1];
			*p++ = rgb[2];
#endif
		}
		p0 += gli_image_s;
	}
}

static int charkern(font_t *f, int c0, int c1)
{
	FT_Vector v;
	int err;
	int g0, g1;

	g0 = FT_Get_Char_Index(f->face, touni(c0));
	g1 = FT_Get_Char_Index(f->face, touni(c1));

	if (g0 == 0 || g1 == 0)
		return 0;

	err = FT_Get_Kerning(f->face, g0, g1, FT_KERNING_UNFITTED, &v);
	if (err)
		winabort("FT_Get_Kerning");

	return (v.x * GLI_SUBPIX) / 64.0;
}


int gli_string_width(int fidx, unsigned char *s, int n, int spw)
{
	font_t *f = &gfont_table[fidx];
	int dolig = ! FT_IS_FIXED_WIDTH(f->face);
	int prev = -1;
	int w = 0;

	if ( FT_Get_Char_Index(f->face, 0xFB01) == 0 )
		dolig = 0;
	if ( FT_Get_Char_Index(f->face, 0xFB02) == 0 )
		dolig = 0;

	while (n--)
	{
		int c = *s++;

		if (dolig && n && c == 'f' && *s == 'i') { c = LIG_FI; s++; n--; }
		if (dolig && n && c == 'f' && *s == 'l') { c = LIG_FL; s++; n--; }

		if (!f->loaded[c])
			loadglyph(f, c);

		if (prev != -1)
			w += charkern(f, prev, c);

		if (spw >= 0 && c == ' ')
			w += spw;
		else
			w += f->advs[c];

		prev = c;
	}

	return w;
}

int gli_draw_string(int x, int y, int fidx, unsigned char *rgb,
		unsigned char *s, int n, int spw)
{
	font_t *f = &gfont_table[fidx];
	int dolig = ! FT_IS_FIXED_WIDTH(f->face);
	int prev = -1;
	int c;
	int px, sx;

	if ( FT_Get_Char_Index(f->face, 0xFB01) == 0 )
		dolig = 0;
	if ( FT_Get_Char_Index(f->face, 0xFB02) == 0 )
		dolig = 0;

	while (n--)
	{
		c = *s++;

		if (dolig && n && c == 'f' && *s == 'i') { c = LIG_FI; s++; n--; }
		if (dolig && n && c == 'f' && *s == 'l') { c = LIG_FL; s++; n--; }

		if (!f->loaded[c])
			loadglyph(f, c);

		if (prev != -1)
			x += charkern(f, prev, c);

		px = x / GLI_SUBPIX;
		sx = x % GLI_SUBPIX;

                if (gli_conf_lcd)
                    draw_bitmap_lcd(&f->glyphs[c][sx], px, y, rgb);
                else
                    draw_bitmap(&f->glyphs[c][sx], px, y, rgb);

		if (spw >= 0 && c == ' ')
			x += spw;
		else
			x += f->advs[c];

		prev = c;
	}

	return x;
}

void gli_draw_caret(int x, int y)
{
	x = x / GLI_SUBPIX;
	if (gli_caret_shape == 0)
	{
		gli_draw_rect(x+0, y+1, 1, 1, gli_caret_color);
		gli_draw_rect(x-1, y+2, 3, 1, gli_caret_color);
		gli_draw_rect(x-2, y+3, 5, 1, gli_caret_color);
	}
	else if (gli_caret_shape == 1)
	{
		gli_draw_rect(x+0, y+1, 1, 1, gli_caret_color);
		gli_draw_rect(x-1, y+2, 3, 1, gli_caret_color);
		gli_draw_rect(x-2, y+3, 5, 1, gli_caret_color);
		gli_draw_rect(x-3, y+4, 7, 1, gli_caret_color);
	}
	else if (gli_caret_shape == 2)
		gli_draw_rect(x+0, y-gli_baseline+1, 1, gli_leading-2, gli_caret_color);
	else if (gli_caret_shape == 3)
		gli_draw_rect(x+0, y-gli_baseline+1, 2, gli_leading-2, gli_caret_color);
	else
		gli_draw_rect(x+0, y-gli_baseline+1, gli_cellw, gli_leading-2, gli_caret_color);
}

void gli_draw_picture(picture_t *src, int x0, int y0, int dx0, int dy0, int dx1, int dy1)
{
	unsigned char *sp, *dp;
	int x1, y1, sx0, sy0, sx1, sy1;
	int x, y, w, h;

	sx0 = 0;
	sy0 = 0;
	sx1 = src->w;
	sy1 = src->h;

	x1 = x0 + src->w;
	y1 = y0 + src->h;

	if (x1 <= dx0 || x0 >= dx1) return;
	if (y1 <= dy0 || y0 >= dy1) return;
	if (x0 < dx0) { sx0 += dx0 - x0; x0 = dx0; }
	if (y0 < dy0) { sy0 += dy0 - y0; y0 = dy0; }
	if (x1 > dx1) { sx1 += dx1 - x1; x1 = dx1; }
	if (y1 > dy1) { sy1 += dy1 - y1; y1 = dy1; }

	sp = src->rgba + (sy0 * src->w + sx0) * 4;
	dp = gli_image_rgb + y0 * gli_image_s + x0 * 3;

	w = sx1 - sx0;
	h = sy1 - sy0;

	for (y = 0; y < h; y++)
	{
		for (x = 0; x < w; x++)
		{
			unsigned char sa = sp[x*4+3];
			unsigned char na = 255 - sa;
			unsigned char sr = mul255(sp[x*4+0], sa);
			unsigned char sg = mul255(sp[x*4+1], sa);
			unsigned char sb = mul255(sp[x*4+2], sa);
#ifdef WIN32
			dp[x*3+0] = sb + mul255(dp[x*3+0], na);
			dp[x*3+1] = sg + mul255(dp[x*3+1], na);
			dp[x*3+2] = sr + mul255(dp[x*3+2], na);
#else
			dp[x*3+0] = sr + mul255(dp[x*3+0], na);
			dp[x*3+1] = sg + mul255(dp[x*3+1], na);
			dp[x*3+2] = sb + mul255(dp[x*3+2], na);
#endif
		}
		sp += src->w * 4;
		dp += gli_image_s;
	}
}
