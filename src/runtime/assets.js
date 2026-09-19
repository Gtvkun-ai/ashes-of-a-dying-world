// Danh sách asset browser dùng chung. Giữ ở runtime để world renderer chỉ nhận asset đã load.
export const ASSET_SOURCES = {
  hyou: 'assets/ashes/hyou-walk.png',
  hyouDiag: 'assets/ashes/hyou-walk-diagonal.png',
  field: 'assets/ashes/field-01.png',
  grassFieldDetail: 'assets/ashes/environment/field-grass-field-detail.png',
  grassEdgeDetail: 'assets/ashes/environment/field-grass-edge-detail.png',
  treeTrunk: 'assets/ashes/environment/tree-trunk.png',
  treeCanopy: 'assets/ashes/environment/tree-canopy.png',
  appleTreeTrunk: 'assets/ashes/environment/apple-tree-trunk.png',
  appleTreeCanopy: 'assets/ashes/environment/apple-tree-canopy.png',
  floraSmall: 'assets/ashes/environment/flora-wind-small.png',
  grassPatch: 'assets/ashes/environment/grass-patch-wind.png',
  floraShadow: 'assets/ashes/environment/flora-contact-shadow.png',
};

export function loadAssets(sources = ASSET_SOURCES) {
  const entries = Object.entries(sources);
  return Promise.all(
    entries.map(([key, src]) =>
      new Promise((resolve, reject) => {
        const image = new Image();
        image.onload = () => resolve([key, image]);
        image.onerror = () => reject(new Error(`Failed to load ${src}`));
        image.src = src;
      }),
    ),
  ).then((loaded) => Object.fromEntries(loaded));
}
