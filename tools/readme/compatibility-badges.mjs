// Domain labels for generated description images; native MDAST owns syntax.
import { fromMarkdown } from "mdast-util-from-markdown";
import { toMarkdown } from "mdast-util-to-markdown";
import { gfm } from "micromark-extension-gfm";
import { gfmFromMarkdown, gfmToMarkdown } from "mdast-util-gfm";

const badgeBase =
  "https://raw.githubusercontent.com/MaksymShostak/oxygen-not-included/main/docs/badges/";
const labels = new Map(
  [
    ["VanillaYes.png", "Base game supported"],
    ["Dlc1Yes.png", "Spaced Out! supported"],
    ["Dlc2Yes.png", "The Frosty Planet Pack supported"],
    ["Dlc3Yes.png", "The Bionic Booster Pack supported"],
    ["Dlc4Yes.png", "The Prehistoric Planet Pack supported"],
    ["Dlc5Yes.png", "The Aquatic Planet Pack supported"],
  ].map(([file, label]) => [badgeBase + file, label]),
);

export function labelCompatibilityImages(markdown) {
  const tree = fromMarkdown(markdown, {
    extensions: [gfm()],
    mdastExtensions: [gfmFromMarkdown()],
  });
  const pending = [tree];
  let changed = false;
  while (pending.length) {
    const node = pending.pop();
    if (node.type === "image" && !node.alt && labels.has(node.url)) {
      node.alt = labels.get(node.url);
      changed = true;
    }
    if (node.children) pending.push(...node.children);
  }
  return changed
    ? toMarkdown(tree, {
        extensions: [gfmToMarkdown()],
        fences: true,
        emphasis: "_",
      })
    : markdown;
}
