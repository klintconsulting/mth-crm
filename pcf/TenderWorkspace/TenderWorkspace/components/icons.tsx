import * as React from "react";

// Lightweight icons. @fluentui/react-icons is not a platform library and pulls in @griffel/react,
// which needs react/jsx-runtime (React 17+) - not available with the platform's React 16.14.
interface GlyphProps { style?: React.CSSProperties; onClick?: () => void }

const glyph = (char: string, label?: string, style?: React.CSSProperties): React.FC<GlyphProps> =>
    function GlyphIcon({ style: extra, onClick }: GlyphProps) {
        return <span aria-hidden={label ? undefined : true} aria-label={label} onClick={onClick}
            style={{ fontSize: 16, lineHeight: "20px", display: "inline-block", minWidth: 16, textAlign: "center", ...style, ...extra }}>{char}</span>;
    };

export const ArrowClockwise20Regular = glyph("⟳");
export const Copy20Regular = glyph("⧉");
export const Mail20Regular = glyph("✉");
export const PersonAdd20Regular = glyph("＋");
export const Delete20Regular = glyph("✕", undefined, { fontSize: 13 });
export const Edit20Regular = glyph("✎");
export const Dismiss20Regular = glyph("✕", undefined, { fontSize: 14 });
export const Dismiss16Regular = glyph("✕", undefined, { fontSize: 11, minWidth: 12, cursor: "pointer" });
export const Search20Regular = glyph("⌕", undefined, { fontSize: 18 });
export const Attach20Regular = glyph("📎", undefined, { fontSize: 14 });
export const TextBold20Regular = glyph("B", undefined, { fontWeight: 700 });
export const TextItalic20Regular = glyph("I", undefined, { fontStyle: "italic", fontFamily: "Georgia, serif" });
export const TextBulletListLtr20Regular = glyph("•≡", undefined, { fontSize: 13 });
